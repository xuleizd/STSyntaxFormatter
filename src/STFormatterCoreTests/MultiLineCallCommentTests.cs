using Xunit;
using Xunit.Abstractions;
using STFormatterCore.Configuration;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Formatter;

namespace STFormatterCoreTests
{
    /// <summary>
    /// Layout of a call whose argument list spans several lines, with and without
    /// commented-out arguments. Two defects are pinned here: a continuation line
    /// used to receive the comma's space in front of its first token, so every
    /// argument sat one column to the right of the first; and a commented-out
    /// argument used to open a blank line after itself, because a line holding
    /// nothing but indentation was taken for a line that already had code on it.
    /// The comment text itself is normalized too — exactly one space after '//' —
    /// so the padding TwinCAT's comment toggle leaves behind cannot survive.
    /// </summary>
    public class MultiLineCallCommentTests
    {
        private readonly ITestOutputHelper _out;
        public MultiLineCallCommentTests(ITestOutputHelper o) { _out = o; }

        private static string Format(string source, bool keepEmptyLines)
        {
            var options = new FormatterOptions { KeepEmptyLines = keepEmptyLines, LineEnding = LineEnding.LF };
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            return new STFormatter(options).Format(cst, source);
        }

        /// <summary>
        /// Pins the exact layout plus the fixed point in both blank-line modes.
        /// </summary>
        private void AssertLayout(string source, string expected)
        {
            foreach (var keep in new[] { true, false })
            {
                var once = Format(source, keep);
                _out.WriteLine($"KeepEmptyLines={keep}:");
                _out.WriteLine(once);
                Assert.True(once == expected,
                    $"KeepEmptyLines={keep}: unexpected layout\n--- expected ---\n{expected}\n--- actual ---\n{once}");
                Assert.True(Format(once, keep) == once,
                    $"KeepEmptyLines={keep}: formatting twice must be a fixed point\n{once}");
            }
        }

        [Fact]
        public void CommentedArguments_InIfCondition_GetNoBlankLine_AndAlignWithArguments()
        {
            AssertLayout(
                "IF cmd.ExecuteDataReturn(\n" +
                "    hDBID := dbId,\n" +
                "    //             pData := ADR(search),\n" +
                "    //             cbData := SIZEOF(search),\n" +
                "    //             pParameter := ADR(para),\n" +
                "    nStartIndex := 0) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n",
                "IF cmd.ExecuteDataReturn(\n" +
                "    hDBID := dbId,\n" +
                "    // pData := ADR(search),\n" +
                "    // cbData := SIZEOF(search),\n" +
                "    // pParameter := ADR(para),\n" +
                "    nStartIndex := 0) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n");
        }

        [Fact]
        public void CommentedArgument_PaddingAfterTheSlashesIsCollapsedToOneSpace()
        {
            // TwinCAT's comment toggle prepends "//" at column 0 and leaves the
            // line's original indentation inside the comment text. A comment is
            // formatted like any other line, so that fossilized padding collapses
            // to the single space every '//' is followed by.
            var result = Format(
                "IF cmd.ExecuteDataReturn(\n" +
                "    hDBID := dbId,\n" +
                "    //             pData := ADR(search),\n" +
                "    nStartIndex := 0) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n",
                keepEmptyLines: false);

            Assert.Contains("// pData := ADR(search),", result);
            Assert.DoesNotContain("//  ", result);
        }

        [Fact]
        public void ContinuationArguments_StayInTheColumnOfTheFirstArgument()
        {
            AssertLayout(
                "IF cmd.Connect(\n" +
                "    sNetID := '',\n" +
                "    sServerName := server,\n" +
                "    pConfigID := ADR(dbId)) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n",
                "IF cmd.Connect(\n" +
                "    sNetID := '',\n" +
                "    sServerName := server,\n" +
                "    pConfigID := ADR(dbId)) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n");
        }

        [Fact]
        public void ContinuationArguments_AlreadyDriftedRight_ArePulledBack()
        {
            // What a run of the buggy version left behind: one extra space on every
            // continuation line that follows a comma.
            AssertLayout(
                "IF cmd.Connect(\n" +
                "    sNetID := '',\n" +
                "     sServerName := server,\n" +
                "     pConfigID := ADR(dbId)) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n",
                "IF cmd.Connect(\n" +
                "    sNetID := '',\n" +
                "    sServerName := server,\n" +
                "    pConfigID := ADR(dbId)) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n");
        }

        [Fact]
        public void CommentAtColumnZero_MovesToTheContinuationIndent()
        {
            AssertLayout(
                "IF cmd.ExecuteDataReturn(\n" +
                "    hDBID := dbId,\n" +
                "//             pData := ADR(search),\n" +
                "nStartIndex := 0) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n",
                "IF cmd.ExecuteDataReturn(\n" +
                "    hDBID := dbId,\n" +
                "    // pData := ADR(search),\n" +
                "    nStartIndex := 0) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n");
        }

        [Fact]
        public void CommentedArgument_InPlainStatementCall_GetsItsOwnLine()
        {
            AssertLayout(
                "cmd.Execute(\n" +
                "    a := 1,\n" +
                "    // b := 2,\n" +
                "    c := 3);\n",
                "cmd.Execute(a := 1,\n" +
                "    // b := 2,\n" +
                "    c := 3);\n");
        }

        [Fact]
        public void CommentAsFirstArgument_KeepsTheBreakAfterTheOpenParen()
        {
            AssertLayout(
                "cmd.Execute(\n" +
                "    // a := 1,\n" +
                "    b := 2);\n",
                "cmd.Execute(\n" +
                "    // a := 1,\n" +
                "    b := 2);\n");
        }

        [Fact]
        public void BlankLineInsideAPlainStatementCall_IsDroppedInBothModes()
        {
            // A statement's own token list is reflowed onto one line, so a blank
            // line inside it is not a separator the user placed between statements.
            const string expected =
                "cmd.Execute(a := 1,\n" +
                "    // b := 2,\n" +
                "    c := 3);\n";

            var source =
                "cmd.Execute(\n" +
                "    a := 1,\n" +
                "    // b := 2,\n" +
                "\n" +
                "    c := 3);\n";

            foreach (var keep in new[] { true, false })
                Assert.Equal(expected, Format(source, keep));
        }

        [Fact]
        public void LineCommentAfterABlockComment_KeepsTheStatementIndent()
        {
            // A block comment fills its line without being code. Reading it as code
            // pushed the line comment — and the statement behind it — one indent
            // level deeper than the block comment they follow.
            AssertLayout(
                "a := 1;\n" +
                "(*note*)\n" +
                "//fb.Velocity := 20;\n" +
                "fb.bAbsX := TRUE;\n",
                "a := 1;\n" +
                "(*note*)\n" +
                "// fb.Velocity := 20;\n" +
                "fb.bAbsX := TRUE;\n");
        }

        [Fact]
        public void LineCommentAfterABlockComment_InsideABody_KeepsTheBodyIndent()
        {
            AssertLayout(
                "IF x THEN\n" +
                "(*note*)\n" +
                "//c\n" +
                "y := 1;\n" +
                "END_IF\n",
                "IF x THEN\n" +
                "    (*note*)\n" +
                "    // c\n" +
                "    y := 1;\n" +
                "END_IF\n");
        }

        [Fact]
        public void BlankLineRunBetweenCommentLines_FollowsTheKeepEmptyLinesPolicy()
        {
            var source =
                "a := 1;\n" +
                "//c1\n" +
                "\n" +
                "\n" +
                "//c2\n" +
                "b := 2;\n";

            // Kept in full when the user asked for every empty line to survive,
            // merged to a single one otherwise. Neither mode may drop the run
            // outright or double it.
            Assert.Equal(
                "a := 1;\n// c1\n\n\n// c2\nb := 2;\n",
                Format(source, keepEmptyLines: true));
            Assert.Equal(
                "a := 1;\n// c1\n\n// c2\nb := 2;\n",
                Format(source, keepEmptyLines: false));
        }
    }
}
