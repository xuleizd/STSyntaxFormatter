using System;
using System.IO;
using System.Linq;
using Xunit;
using STFormatterCLI;
using STFormatterCore.Configuration;

namespace STFormatterCLITests
{
    /// <summary>
    /// End-to-end regression on testdata/RepeatUntil.TcPOU. ST puts no ';' after
    /// the UNTIL condition, so the parser used to collect the condition up to the
    /// next semicolon: it swallowed END_REPEAT together with every statement that
    /// followed the loop and re-emitted them in the wrong place, and the formatter
    /// then invented a ';' of its own. No sample in the rest of the corpus uses
    /// REPEAT, so this fixture is the only integration-level cover for it. All
    /// work happens on a temp copy.
    /// </summary>
    public class RepeatUntilRegression
    {
        private const string ExpectedSt =
            "REPEAT\r\n" +
            "    a := a - 1;\r\n" +
            "UNTIL a <= 0\r\n" +
            "END_REPEAT\r\n" +
            "\r\n" +
            "IF a > 0 THEN\r\n" +
            "    IF a > 5 THEN\r\n" +
            "        a := 1;\r\n" +
            "    END_IF\r\n" +
            "END_IF\r\n" +
            "\r\n" +
            "REPEAT\r\n" +
            "    a := a + 1;\r\n" +
            "    // before UNTIL\r\n" +
            "UNTIL a > 10;\r\n" +
            "END_REPEAT\r\n";

        private static string RepoRoot()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        }

        private static string CopyFixtureToTemp()
        {
            var src = Path.Combine(RepoRoot(), "testdata", "RepeatUntil.TcPOU");
            Assert.True(File.Exists(src), $"fixture not found: {src}");
            var dir = Path.Combine(Path.GetTempPath(), "STFormatterCLITests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var work = Path.Combine(dir, "RepeatUntil.TcPOU");
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
        public void Format_KeepsEndRepeatInPlace_AndDoesNotInventASemicolon(bool keepEmptyLines)
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
                    $"formatting RepeatUntil twice with KeepEmptyLines={keepEmptyLines} must be a fixed point");

                var after = File.ReadAllText(work);

                // Both loops close where they were written, the IF block between
                // them stayed outside them, the UNTIL that had no ';' still has
                // none and the one that had a ';' still has exactly one.
                Assert.Contains(ExpectedSt, after);

                Assert.DoesNotContain(";;", after);
                Assert.Equal(2, CountOccurrences(after, "END_REPEAT"));
            }
            finally
            {
                File.Delete(work);
            }
        }
    }
}
