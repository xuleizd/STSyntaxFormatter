using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using STFormatterCLI;
using STFormatterCore.Configuration;

namespace STFormatterCLITests
{
    /// <summary>
    /// Regression suite that replays real-world TwinCAT files taken from the
    /// TcBlack project's TestData. The acceptance bar mirrors TcBlack's
    /// robustness guarantees:
    ///   1. formatting never changes the meaning — content (tokens/strings/
    ///      comments) must be preserved;
    ///   2. formatting is idempotent — a second run produces byte-identical output.
    /// </summary>
    public class TcBlackTestDataRegression : IDisposable
    {
        private readonly string _tempDir;

        public TcBlackTestDataRegression()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TcBlackRegression_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            // Fixture lives at <repo>/testdata/TcBlackTestData. From bin/Debug/net48
            // walk up to the repo root.
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var root = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            var fixture = Path.Combine(root, "testdata", "TcBlackTestData");
            if (!Directory.Exists(fixture))
                throw new DirectoryNotFoundException($"TcBlack TestData not found at {fixture}");

            CopyDirectory(fixture, _tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); }
                catch (IOException) { /* best effort cleanup */ }
            }
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dir.Replace(source, dest));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, file.Replace(source, dest), true);
        }

        private static FormatterOptions Options() => new FormatterOptions
        {
            IndentSize = 4,
            AlignDeclarations = true,
            KeepEmptyLines = true,
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

        [Fact]
        public void AllFiles_Format_PreservesContentAndIsIdempotent()
        {
            var files = Directory.GetFiles(_tempDir, "*.*", SearchOption.AllDirectories);
            Assert.NotEmpty(files);

            foreach (var file in files)
            {
                if (!TcPouFile.IsSupportedFile(file)) continue;

                // First run must not corrupt content.
                var originalContent = StripWhitespace(CdataText(file));
                var pou = new TcPouFile(file);
                pou.Format(Options());
                pou.Save();

                var firstContent = StripWhitespace(CdataText(file));
                Assert.Equal(originalContent, firstContent);

                var firstBytes = File.ReadAllBytes(file);

                // Second run must be byte-identical (idempotency).
                pou = new TcPouFile(file);
                pou.Format(Options());
                pou.Save();
                var secondBytes = File.ReadAllBytes(file);

                Assert.True(
                    ByteArraysEqual(firstBytes, secondBytes),
                    $"Idempotency failed for {Path.GetFileName(file)}");
            }
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
