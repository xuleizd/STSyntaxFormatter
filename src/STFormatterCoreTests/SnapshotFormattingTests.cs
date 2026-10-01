using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using STFormatterCore.Configuration;
using STFormatterCore.Formatter;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Validation;
using Xunit;
using Xunit.Sdk;

namespace STFormatterCoreTests
{
    /// <summary>
    /// Snapshot formatting tests, organized the CSharpier way: one .test file per
    /// syntax construct. The .test file is the (deliberately untidy) input; the
    /// .expected.test file next to it is the exact output the formatter must
    /// produce. Every case asserts three invariants:
    ///   1. Format(input) == expected                       — the layout is pinned
    ///   2. Validate(input, actual) passes                  — no token/tree/comment corruption
    ///   3. Format(actual) == actual                        — formatting is idempotent
    ///
    /// Workflow:
    ///   - add a case: write X.test (input only), then run once with
    ///     STF_SNAPSHOT_UPDATE=1 to generate X.expected.test, review the generated
    ///     file (it becomes the contract!) and commit both.
    ///   - on failure the actual output is written to X.actual.test (git-ignored)
    ///     and a unified diff is embedded in the failure message; set
    ///     STF_SHOW_DIFF=1 (plus STF_DIFF_TOOL="C:\path\tool.exe" or a command
    ///     line like "code --diff" if WinMerge is not installed) to pop a diff
    ///     viewer on it.
    /// </summary>
    public class SnapshotFormattingTests
    {
        private static string SnapshotsRoot()
        {
            // TestAssembly/bin/Debug/net48 → up to src/ → STFormatterCoreTests/Snapshots
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "Snapshots"));
        }

        public static IEnumerable<object[]> AllSnapshotFiles()
        {
            var root = SnapshotsRoot();
            if (!Directory.Exists(root))
            {
                yield return new object[] { "__MISSING__", "__MISSING__" };
                yield break;
            }

            foreach (var testFile in Directory.GetFiles(root, "*.test", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith(".expected.test", StringComparison.OrdinalIgnoreCase) &&
                            !p.EndsWith(".actual.test", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(testFile);
                var category = Path.GetFileName(Path.GetDirectoryName(testFile));
                yield return new object[] { $"{category}/{name}", testFile };
            }
        }

        [Theory]
        [MemberData(nameof(AllSnapshotFiles))]
        public void Snapshot_Matches_AndIsValid_AndIdempotent(string displayName, string testFilePath)
        {
            var options = new FormatterOptions { LineEnding = LineEnding.LF };
            var input = File.ReadAllText(testFilePath).Replace("\r\n", "\n");
            var expectedPath = Path.ChangeExtension(testFilePath, ".expected.test");

            string FormatOnce(string source)
            {
                var tokens = new STLexer(source).Tokenize();
                var cst = new STParser(tokens).Parse();
                return new STFormatter(options).Format(cst, source);
            }

            string actual = FormatOnce(input);

            if (Environment.GetEnvironmentVariable("STF_SNAPSHOT_UPDATE") == "1")
            {
                File.WriteAllText(expectedPath, actual, new UTF8Encoding(false));
                return; // review the generated file with git diff before committing
            }

            if (!File.Exists(expectedPath))
                throw new XunitException(
                    $"缺少期望文件 {Path.GetFileName(expectedPath)}。运行一次 STF_SNAPSHOT_UPDATE=1 的测试生成它，人工审查后一并提交。");

            string expected = File.ReadAllText(expectedPath).Replace("\r\n", "\n");

            if (actual != expected)
            {
                var actualPath = Path.ChangeExtension(testFilePath, ".actual.test");
                File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
                TryLaunchDiffTool(expectedPath, actualPath);
                throw new XunitException(
                    $"快照不一致：{displayName}\n--- expected ---\n{expected}\n--- actual（已写入 {Path.GetFileName(actualPath)}）---\n{actual}\n--- diff ---\n{SimpleDiff.Unified(expected, actual)}");
            }

            var validation = FormattingValidator.Validate(input, actual, options);
            if (!validation.IsValid)
                throw new XunitException($"格式化结果未通过等价性校验：{displayName}\n{validation.FailureMessage}");

            var twice = FormatOnce(actual);
            if (twice != actual)
                throw new XunitException(
                    $"格式化不幂等：{displayName}\n--- 第一遍 ---\n{actual}\n--- 第二遍 ---\n{twice}");
        }

        private static void TryLaunchDiffTool(string expectedPath, string actualPath)
        {
            try
            {
                if (Environment.GetEnvironmentVariable("STF_SHOW_DIFF") != "1") return;

                var commandLine = Environment.GetEnvironmentVariable("STF_DIFF_TOOL");
                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    var winMerge = new[]
                    {
                        @"C:\Program Files\WinMerge\WinMerge.exe",
                        @"C:\Program Files (x86)\WinMerge\WinMerge.exe"
                    }.FirstOrDefault(File.Exists);
                    if (winMerge == null) return;
                    commandLine = $"\"{winMerge}\"";
                }

                var separator = commandLine.IndexOf(' ');
                var exe = separator < 0 ? commandLine : commandLine.Substring(0, separator);
                var args = (separator < 0 ? "" : commandLine.Substring(separator + 1)) +
                           $" \"{expectedPath}\" \"{actualPath}\"";
                if (File.Exists(exe))
                    Process.Start(exe, args);
            }
            catch
            {
                // A diff viewer is a convenience; never let it fail the test.
            }
        }
    }

    /// <summary>
    /// Small dependency-free unified diff for snapshot failure messages. Plain
    /// LCS table + backtrack; snapshot files are small, oversized inputs fall
    /// back to a size notice.
    /// </summary>
    internal static class SimpleDiff
    {
        public static string Unified(string expected, string actual)
        {
            var left = expected.Split('\n');
            var right = actual.Split('\n');
            if (left.Length + right.Length > 1000)
                return $"（文件过大，diff 省略：expected {left.Length} 行 / actual {right.Length} 行，直接看 .actual.test）";

            // dp[i, j] = LCS length of left[i..] and right[j..]
            var dp = new int[left.Length + 1, right.Length + 1];
            for (int i = left.Length - 1; i >= 0; i--)
            {
                for (int j = right.Length - 1; j >= 0; j--)
                {
                    dp[i, j] = LineEquals(left[i], right[j])
                        ? dp[i + 1, j + 1] + 1
                        : Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }

            var output = new List<string>();
            int x = 0, y = 0;
            while (x < left.Length && y < right.Length)
            {
                if (LineEquals(left[x], right[y]))
                {
                    output.Add("  " + left[x].TrimEnd());
                    x++; y++;
                }
                else if (dp[x + 1, y] >= dp[x, y + 1])
                {
                    output.Add("- " + left[x].TrimEnd());
                    x++;
                }
                else
                {
                    output.Add("+ " + right[y].TrimEnd());
                    y++;
                }
            }
            while (x < left.Length) { output.Add("- " + left[x].TrimEnd()); x++; }
            while (y < right.Length) { output.Add("+ " + right[y].TrimEnd()); y++; }

            var trimmed = output.Take(80).ToList();
            if (output.Count > 80) trimmed.Add($"…（共 {output.Count} 行差异输出）");
            return string.Join("\n", trimmed);
        }

        private static bool LineEquals(string a, string b) =>
            string.Equals(a.TrimEnd(), b.TrimEnd(), StringComparison.Ordinal);
    }
}
