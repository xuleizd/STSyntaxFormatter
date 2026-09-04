using System;
using System.IO;
using System.Linq;
using Xunit;
using STFormatterCLI;
using STFormatterCore.Configuration;

namespace STFormatterCLITests
{
    /// <summary>
    /// End-to-end regression on testdata/DBBusinessUpdate.TcDUT, the file the
    /// defect was reported against: the commented-out STRUCT members sitting
    /// directly above END_STRUCT fell back to column 0 while the commented-out
    /// member at the top of the STRUCT kept its indent. All work happens on a
    /// temp copy — the fixture itself is never modified.
    /// </summary>
    public class DbBusinessUpdateRegression
    {
        private static string RepoRoot()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        }

        private static string CopyFixtureToTemp()
        {
            var src = Path.Combine(RepoRoot(), "testdata", "DBBusinessUpdate.TcDUT");
            Assert.True(File.Exists(src), $"fixture not found: {src}");
            var dir = Path.Combine(Path.GetTempPath(), "STFormatterCLITests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var work = Path.Combine(dir, "DBBusinessUpdate.TcDUT");
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
        public void Format_KeepsTheCommentedOutMembersAtTheMemberIndent(bool keepEmptyLines)
        {
            var work = CopyFixtureToTemp();
            try
            {
                var before = File.ReadAllText(work);
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
                    $"formatting DBBusinessUpdate twice with KeepEmptyLines={keepEmptyLines} must be a fixed point");

                var after = File.ReadAllText(work);

                // The reported defect: these two sat at column 0.
                Assert.Contains(
                    "    // ISFG     : BOOL;\r\n" +
                    "    // DATA01   : LREAL;\r\n" +
                    "END_STRUCT",
                    after);

                // Nothing was dropped: the pragma, both type keywords and every
                // member — declared or commented out — survive.
                Assert.Contains("{attribute 'pack_mode' := '1'}", after);
                Assert.Contains("TYPE DBBusinessUpdate :", after);
                Assert.Contains("END_STRUCT", after);
                Assert.Contains("END_TYPE", after);
                foreach (var member in new[] { "PLANNO", "USERNAME", "KSSJ", "WC", "ISFG", "DATA01", "ID" })
                    Assert.Contains(member, after);
                Assert.Equal(CountOccurrences(before, ";"), CountOccurrences(after, ";"));
                Assert.Equal(CountOccurrences(before, "//"), CountOccurrences(after, "//"));
            }
            finally
            {
                File.Delete(work);
            }
        }
    }
}
