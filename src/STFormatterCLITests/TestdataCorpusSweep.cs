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
        /// (comment text hanging off a broken VAR line). Formatting them would
        /// lose those comments, so since 1.8.8 the equivalence validator refuses
        /// them outright — refusal is the correct outcome for these samples, and
        /// any OTHER sample being refused is a regression. Before the validator
        /// existed these files were silently written back with comments missing.
        /// </summary>
        private static readonly HashSet<string> KnownCorruptSamples = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
            var refusedCorrupt = new List<string>();
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
                catch (InvalidOperationException ex) when (ex.Message.Contains("等价性校验未通过"))
                {
                    // The equivalence validator refused the file. That is the
                    // correct outcome for the known-corrupt samples (writing them
                    // back used to silently drop comments); for anything else it
                    // is a regression.
                    if (KnownCorruptSamples.Contains(Path.GetFileName(name)))
                        refusedCorrupt.Add(name);
                    else
                        errors.Add($"{name}: {ex.GetType().Name} {ex.Message}");
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
            _out.WriteLine($"REFUSED_CORRUPT {refusedCorrupt.Count}");
            foreach (var c in refusedCorrupt) _out.WriteLine("  REFUSED " + c);
            _out.WriteLine($"ERRORS {errors.Count}");
            foreach (var c in errors) _out.WriteLine("  ERROR " + c);

            Assert.True(errors.Count == 0,
                "formatting threw on real-world samples:\n" + string.Join("\n", errors));

            Assert.True(notFixedPoint.Count == 0,
                "formatting is not a fixed point on:\n" + string.Join("\n", notFixedPoint));

            var unexpectedLoss = lostComments
                .Where(entry => !KnownCorruptSamples.Contains(Path.GetFileName(entry.Split(' ')[0])))
                .ToList();
            Assert.True(unexpectedLoss.Count == 0,
                "comments were dropped from samples that are not already corrupt:\n" +
                string.Join("\n", unexpectedLoss));
        }

        /// <summary>
        /// Byte-exact baseline over the whole corpus: after formatting (once, with
        /// the fixed options above), every sample's resulting file must hash to the
        /// committed baseline in testdata-baseline.txt. Any formatter change that
        /// touches real-world output now shows up as an explicit baseline diff the
        /// author must review and update — mirroring what the snapshot suite does
        /// per construct, but across all ~250 real samples. The known-corrupt
        /// samples are recorded as REFUSED (the validator rejects them, which is
        /// the correct outcome).
        /// Update the baseline with STF_BASELINE_UPDATE=1 and review the git diff.
        /// </summary>
        [Fact]
        public void Corpus_FormattedOutput_MatchesCommittedBaseline()
        {
            var root = Path.Combine(RepoRoot(), "testdata");
            Assert.True(Directory.Exists(root), $"testdata corpus not found at {root}");

            var exts = new[] { ".tcpou", ".tcdut", ".tcgvl" };
            var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !f.Replace('\\', '/').Contains("/testdata/backup/"))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var options = new FormatterOptions
            {
                IndentSize = 4,
                AlignDeclarations = true,
                KeepEmptyLines = true,
                LineEnding = LineEnding.CRLF
            };

            // Lives in the project tree (committed) — NOT next to the assembly,
            // which xUnit shadow-copies to a temp directory.
            var baselinePath = Path.Combine(
                RepoRoot(), "src", "STFormatterCLITests", "testdata-baseline.txt");
            var update = Environment.GetEnvironmentVariable("STF_BASELINE_UPDATE") == "1";

            var baseline = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!update && File.Exists(baselinePath))
            {
                foreach (var line in File.ReadAllLines(baselinePath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    int tab = line.IndexOf('\t');
                    Assert.True(tab > 0, $"malformed baseline line: {line}");
                    baseline[line.Substring(0, tab)] = line.Substring(tab + 1);
                }
            }

            var newBaseline = new List<string>();
            var mismatches = new List<string>();

            foreach (var src in files)
            {
                var name = src.Substring(root.Length + 1).Replace('\\', '/');
                var tmp = Path.Combine(Path.GetTempPath(),
                    "sweepbase_" + Guid.NewGuid().ToString("N") + Path.GetExtension(src));
                try
                {
                    File.Copy(src, tmp);
                    if (!TcPouFile.IsSupportedFile(tmp)) continue;

                    string recorded;
                    try
                    {
                        var pou = new TcPouFile(tmp);
                        pou.Format(options);
                        pou.Save();
                        using (var sha = System.Security.Cryptography.SHA256.Create())
                        using (var stream = File.OpenRead(tmp))
                        {
                            recorded = Convert.ToBase64String(sha.ComputeHash(stream));
                        }
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("等价性校验未通过"))
                    {
                        Assert.True(KnownCorruptSamples.Contains(Path.GetFileName(name)),
                            $"{name} was refused by the validator but is not a known-corrupt sample: {ex.Message}");
                        recorded = "REFUSED";
                    }

                    newBaseline.Add(name + "\t" + recorded);

                    if (update) continue;

                    if (!baseline.TryGetValue(name, out var expected))
                        mismatches.Add($"{name}: not in baseline (run STF_BASELINE_UPDATE=1)");
                    else if (!string.Equals(expected, recorded, StringComparison.Ordinal))
                        mismatches.Add($"{name}: output changed — review and update the baseline");
                }
                finally
                {
                    try { File.Delete(tmp); } catch (IOException) { }
                }
            }

            if (update)
            {
                File.WriteAllLines(baselinePath,
                    new[] { "# testdata corpus output baseline (STF_BASELINE_UPDATE=1 to regenerate)" }
                    .Concat(newBaseline));
                _out.WriteLine($"BASELINE_WRITTEN {newBaseline.Count} entries to {baselinePath}");
                return;
            }

            var stale = baseline.Keys
                .Where(k => !newBaseline.Any(l => l.StartsWith(k + "\t", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            Assert.True(stale.Count == 0,
                "baseline contains samples that no longer exist (regenerate it):\n" +
                string.Join("\n", stale));

            Assert.True(mismatches.Count == 0,
                $"{mismatches.Count} sample(s) no longer match the committed baseline.\n" +
                "If the change is intended, rerun with STF_BASELINE_UPDATE=1 and review the git diff.\n" +
                string.Join("\n", mismatches));
        }
    }
}
