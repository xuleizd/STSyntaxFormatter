using System;
using System.IO;
using System.Linq;
using Xunit;
using STFormatterCLI;
using STFormatterCore.Configuration;

namespace STFormatterCLITests
{
    /// <summary>
    /// End-to-end regression on the real-world testdata/DBManager.TcPOU fixture:
    /// formatting must be idempotent in both KeepEmptyLines modes and must never
    /// destroy code (assignment count and structural keywords survive). All work
    /// happens in a temp copy — the fixture itself is never modified.
    /// </summary>
    public class DbManagerRegression
    {
        private static string RepoRoot()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        }

        private static string CopyFixtureToTemp()
        {
            var src = Path.Combine(RepoRoot(), "testdata", "DBManager.TcPOU");
            var dir = Path.Combine(Path.GetTempPath(), "STFormatterCLITests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var work = Path.Combine(dir, "DBManager.TcPOU");
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
        public void Format_IsIdempotent_AndNonDestructive(bool keepEmptyLines)
        {
            var work = CopyFixtureToTemp();
            try
            {
                var before = File.ReadAllText(work);
                int assignCount = CountOccurrences(before, ":=");

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
                    $"formatting DBManager twice with KeepEmptyLines={keepEmptyLines} must be a fixed point");

                var after = File.ReadAllText(work);
                Assert.True(assignCount > 0, "fixture must contain assignments");
                Assert.Equal(assignCount, CountOccurrences(after, ":="));
                Assert.Contains("FUNCTION_BLOCK DBManager", after);
                Assert.Contains("END_VAR", after);

                // Continuation arguments of a multi-line call stay in the column of
                // the first one. NeedsSpaceBefore only knows the previous token, so
                // the comma's space used to leak onto the start of every wrapped line
                // and push it one column right on each formatting run.
                Assert.Contains(
                    "IF cmdQuery.ExecuteDataReturn(\r\n" +
                    "            hDBID := dbId,\r\n" +
                    "            pExpression := ADR(sqlCmd),\r\n" +
                    "            cbExpression := SIZEOF(sqlCmd),\r\n",
                    after);
            }
            finally
            {
                File.Delete(work);
            }
        }
    }
}
