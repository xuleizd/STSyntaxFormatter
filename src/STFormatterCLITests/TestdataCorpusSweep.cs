using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using STFormatterCLI;
using STFormatterCore.Configuration;

namespace STFormatterCLITests
{
    /// <summary>
    /// Sweeps the whole testdata corpus — every .TcPOU/.TcDUT/.TcGVL under
    /// testdata/, several hundred real TwinCAT files, against the ~50 that the
    /// dedicated fixtures cover — and holds two invariants that must never break:
    /// formatting never throws and never stops being a fixed point, and it never
    /// drops comments outside the handful of samples that are already corrupt in
    /// the repository. The count of files whose whitespace-stripped content
    /// changes is reported but not asserted: those samples carry artifacts such
    /// as a stray ';;' after a VAR member, which the formatter legitimately
    /// normalises away.
    /// </summary>
    public class TestdataCorpusSweep
    {
        /// <summary>
        /// Samples whose declarations are already malformed in the repository
        /// (comment text hanging off a broken VAR line). Comments attached to
        /// those unreachable tokens are lost by this formatter and were lost by
        /// every previous version too, so they are excluded from the guard.
        /// </summary>
        private static readonly HashSet<string> KnownLossySamples = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "_Auto.TcPOU",
            "_Safety.TcPOU"
        };

        private readonly ITestOutputHelper _out;

        public TestdataCorpusSweep(ITestOutputHelper output)
        {
            _out = output;
        }

        private static string RepoRoot()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        }

        private static string StripWhitespace(string s) => Regex.Replace(s, @"\s+", "");

        private static string CdataText(string path)
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            var sb = new StringBuilder();
            foreach (Match m in Regex.Matches(text, @"<!\[CDATA\[(.*?)\]\]>", RegexOptions.Singleline))
                sb.Append(m.Groups[1].Value);
            return sb.ToString();
        }

        private static int CountComments(string s)
        {
            int n = 0;
            for (int i = 0; i + 1 < s.Length; i++)
            {
                if (s[i] == '/' && s[i + 1] == '/') { n++; i++; }
                else if (s[i] == '(' && s[i + 1] == '*') { n++; i++; }
            }
            return n;
        }

        [Fact]
        public void WholeCorpus_NeverThrows_IsAFixedPoint_AndKeepsItsComments()
        {
            var root = Path.Combine(RepoRoot(), "testdata");
            Assert.True(Directory.Exists(root), $"testdata corpus not found at {root}");

            var exts = new[] { ".tcpou", ".tcdut", ".tcgvl" };
            var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !f.Replace('\\', '/').Contains("/testdata/backup/"))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.NotEmpty(files);
            _out.WriteLine($"SAMPLES {files.Count}");

            var options = new FormatterOptions
            {
                IndentSize = 4,
                AlignDeclarations = true,
                KeepEmptyLines = true,
                LineEnding = LineEnding.CRLF
            };

            var changed = new List<string>();
            var lostComments = new List<string>();
            var notFixedPoint = new List<string>();
            var errors = new List<string>();
            int oldFormat = 0;

            foreach (var src in files)
            {
                var name = src.Substring(root.Length + 1).Replace('\\', '/');
                var tmp = Path.Combine(Path.GetTempPath(),
                    "sweep_" + Guid.NewGuid().ToString("N") + Path.GetExtension(src));
                try
                {
                    File.Copy(src, tmp);
                    if (!TcPouFile.IsSupportedFile(tmp)) continue;

                    string original = CdataText(tmp);
                    if (original.Length == 0)
                    {
                        // Legacy <Single>-per-line format: no CDATA to compare.
                        oldFormat++;
                        original = null;
                    }

                    var pou = new TcPouFile(tmp);
                    pou.Format(options);
                    pou.Save();
                    var firstBytes = File.ReadAllBytes(tmp);

                    if (original != null)
                    {
                        string after = CdataText(tmp);
                        if (StripWhitespace(original) != StripWhitespace(after))
                            changed.Add(name);

                        int before = CountComments(original), now = CountComments(after);
                        if (now < before)
                            lostComments.Add($"{name} ({before} -> {now})");
                    }

                    pou = new TcPouFile(tmp);
                    pou.Format(options);
                    pou.Save();
                    if (!firstBytes.SequenceEqual(File.ReadAllBytes(tmp)))
                        notFixedPoint.Add(name);
                }
                catch (Exception ex)
                {
                    errors.Add($"{name}: {ex.GetType().Name} {ex.Message}");
                }
                finally
                {
                    try { File.Delete(tmp); } catch (IOException) { }
                }
            }

            _out.WriteLine($"OLD_FORMAT_SKIPPED {oldFormat}");
            _out.WriteLine($"CONTENT_CHANGED {changed.Count}");
            _out.WriteLine($"COMMENTS_LOST {lostComments.Count}");
            foreach (var c in lostComments) _out.WriteLine("  LOST " + c);
            _out.WriteLine($"NOT_IDEMPOTENT {notFixedPoint.Count}");
            foreach (var c in notFixedPoint) _out.WriteLine("  NOTFIXEDPOINT " + c);
            _out.WriteLine($"ERRORS {errors.Count}");
            foreach (var c in errors) _out.WriteLine("  ERROR " + c);

            Assert.True(errors.Count == 0,
                "formatting threw on real-world samples:\n" + string.Join("\n", errors));

            Assert.True(notFixedPoint.Count == 0,
                "formatting is not a fixed point on:\n" + string.Join("\n", notFixedPoint));

            var unexpectedLoss = lostComments
                .Where(entry => !KnownLossySamples.Contains(Path.GetFileName(entry.Split(' ')[0])))
                .ToList();
            Assert.True(unexpectedLoss.Count == 0,
                "comments were dropped from samples that are not already corrupt:\n" +
                string.Join("\n", unexpectedLoss));
        }
    }
}
