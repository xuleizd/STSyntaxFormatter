using System;
using System.IO;
using System.Linq;
using STFormatterCore.Configuration;
using STFormatterCLI;
using Xunit;

namespace STFormatterCLITests
{
    /// <summary>
    /// End-to-end pin for the "STRUCT RETAIN" bug (1.8.7): TwinCAT 3 declares a
    /// whole struct as retain data with the RETAIN modifier right after STRUCT,
    /// and the formatter used to delete it — ParseStructBody let the keyword fall
    /// into its stray-token branch and VisitStructBody only ever wrote STRUCT and
    /// END_STRUCT. testdata/ has no other sample using STRUCT RETAIN, so this
    /// fixture (testdata/StructRetain.TcDUT) is its only integration-level cover.
    /// </summary>
    public class StructRetainRegression
    {
        private const string ExpectedDeclaration =
            "TYPE ST_RetainCounter :\r\n" +
            "STRUCT RETAIN\r\n" +
            "    nCounter : DINT;\r\n" +
            "    sLabel   : STRING(32);\r\n" +
            "    bEnabled : BOOL;\r\n" +
            "END_STRUCT\r\n" +
            "END_TYPE\r\n";

        private static string RepoRoot()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        }

        private static string CopyFixtureToTemp()
        {
            var src = Path.Combine(RepoRoot(), "testdata", "StructRetain.TcDUT");
            Assert.True(File.Exists(src), $"fixture not found: {src}");
            var dir = Path.Combine(Path.GetTempPath(), "STFormatterCLITests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var work = Path.Combine(dir, "StructRetain.TcDUT");
            File.Copy(src, work);
            return work;
        }

        private static int CountOccurrences(string text, string needle)
        {
            int count = 0, index = 0;
            while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Format_KeepsTheRetainModifierOnTheStructHeader(bool keepEmptyLines)
        {
            var work = CopyFixtureToTemp();
            try
            {
                var options = new FormatterOptions
                {
                    IndentSize = 4,
                    AlignDeclarations = true,
                    KeepEmptyLines = keepEmptyLines,
                    LineEnding = LineEnding.CRLF
                };

                var pou = new TcPouFile(work);
                pou.Format(options);
                pou.Save();
                var firstBytes = File.ReadAllBytes(work);

                pou = new TcPouFile(work);
                pou.Format(options);
                pou.Save();
                var secondBytes = File.ReadAllBytes(work);

                Assert.True(firstBytes.SequenceEqual(secondBytes),
                    $"formatting StructRetain twice with KeepEmptyLines={keepEmptyLines} must be a fixed point");

                var after = File.ReadAllText(work);

                // The retain property must survive formatting: modifier kept exactly
                // once, on the STRUCT header line, layout pinned.
                Assert.Contains(ExpectedDeclaration, after);
                Assert.Equal(1, CountOccurrences(after, "RETAIN"));
            }
            finally
            {
                File.Delete(work);
            }
        }
    }
}
