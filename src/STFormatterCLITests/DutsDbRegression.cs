using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using STFormatterCLI;
using STFormatterCore.Configuration;

namespace STFormatterCLITests
{
    /// <summary>
    /// Replays the real-world DUT fixtures in testdata/DUTs_DB — the .TcDUT shapes
    /// TwinCAT itself produces (type alias with the ';' on its own line, STRUCT /
    /// UNION bodies indented inconsistently, multi-line enum, "END_TYPE;", pragma
    /// attributes). Acceptance bar: formatting preserves content and is a fixed
    /// point, in both KeepEmptyLines modes. All work happens on temp copies.
    /// </summary>
    public class DutsDbRegression : IDisposable
    {
        private readonly string _tempDir;

        public DutsDbRegression()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "DutsDbRegression_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var root = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            var fixture = Path.Combine(root, "testdata", "DUTs_DB");
            if (!Directory.Exists(fixture))
                throw new DirectoryNotFoundException($"DUT fixtures not found at {fixture}");

            foreach (var file in Directory.GetFiles(fixture, "*.TcDUT"))
                File.Copy(file, Path.Combine(_tempDir, Path.GetFileName(file)));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); }
                catch (IOException) { /* best effort cleanup */ }
            }
        }

        private static FormatterOptions Options(bool keepEmptyLines) => new FormatterOptions
        {
            IndentSize = 4,
            AlignDeclarations = true,
            KeepEmptyLines = keepEmptyLines,
            LineEnding = LineEnding.CRLF
        };

        private static string StripWhitespace(string s) => Regex.Replace(s, @"\s+", "");

        private static string CdataText(string path)
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            var sb = new StringBuilder();
            foreach (Match m in Regex.Matches(text, @"<!\[CDATA\[(.*?)\]\]>", RegexOptions.Singleline))
                sb.Append(m.Groups[1].Value);
            return sb.ToString();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AllDutFiles_Format_PreservesContentAndIsIdempotent(bool keepEmptyLines)
        {
            var files = Directory.GetFiles(_tempDir, "*.TcDUT");
            Assert.NotEmpty(files);

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var options = Options(keepEmptyLines);

                var originalContent = StripWhitespace(CdataText(file));
                var pou = new TcPouFile(file);
                pou.Format(options);
                pou.Save();

                var firstContent = StripWhitespace(CdataText(file));
                Assert.True(originalContent == firstContent,
                    $"KeepEmptyLines={keepEmptyLines}: formatting {name} changed its content");

                // A DUT declaration must still declare its type once formatted.
                Assert.Contains("END_TYPE", CdataText(file));

                var firstBytes = File.ReadAllBytes(file);

                pou = new TcPouFile(file);
                pou.Format(options);
                pou.Save();
                var secondBytes = File.ReadAllBytes(file);

                Assert.True(firstBytes.SequenceEqual(secondBytes),
                    $"KeepEmptyLines={keepEmptyLines}: formatting {name} twice must be a fixed point");
            }
        }
    }
}
