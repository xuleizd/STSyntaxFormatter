using Xunit;
using Xunit.Abstractions;
using STFormatterCore.Configuration;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Formatter;

namespace STFormatterCoreTests
{
    /// <summary>
    /// A comment is formatted, not copied. Three rules:
    ///   1. exactly one space follows '//';
    ///   2. a comment behind code on the same line sits 4 spaces away from it;
    ///   3. a comment on a line of its own takes the indent of the line BELOW it.
    /// Plus the invariant the rules must not break: formatting never deletes a
    /// comment. Three paths used to do exactly that — a comment on the last line
    /// of the source was filed as leading trivia of EOF, comment lines after the
    /// last construct were discarded with the EOF token, and a comment on a CASE
    /// label was never read because the label only looked at leading trivia.
    /// </summary>
    public class CommentFormattingTests
    {
        private readonly ITestOutputHelper _out;
        public CommentFormattingTests(ITestOutputHelper o) { _out = o; }

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

        // ---- rule 1: exactly one space after '//' ----

        [Fact]
        public void SpaceAfterTheSlashes_IsCollapsedToOne()
        {
            AssertLayout(
                "//no space\n" +
                "//      padded\n" +
                "//\ttabbed\n" +
                "// just right\n" +
                "a := 1;\n",
                "// no space\n" +
                "// padded\n" +
                "// tabbed\n" +
                "// just right\n" +
                "a := 1;\n");
        }

        [Fact]
        public void BareSlashes_StayBare()
        {
            // An empty comment is a separator the author placed on purpose; it gets
            // no trailing space (RemoveTrailingWhitespace would strip it anyway).
            AssertLayout(
                "a := 1;\n" +
                "//\n" +
                "b := 2;\n",
                "a := 1;\n" +
                "//\n" +
                "b := 2;\n");
        }

        [Fact]
        public void TripleSlash_IsAMarkerOfItsOwn_AndIsNotSplit()
        {
            // '///' is normalized after the third slash. A '/' that only follows
            // padding is content, not a marker: '//  / x' must not become '/// x'.
            AssertLayout(
                "///doc\n" +
                "//  / x\n" +
                "a := 1;\n",
                "/// doc\n" +
                "// / x\n" +
                "a := 1;\n");
        }

        // ---- rule 2: a trailing comment sits 4 spaces after the code ----

        [Fact]
        public void TrailingComment_SitsFourSpacesAfterTheSemicolon()
        {
            AssertLayout(
                "a := 1; // one space\n" +
                "b := 2;       // many spaces\n" +
                "c := 3;//glued\n",
                "a := 1;    // one space\n" +
                "b := 2;    // many spaces\n" +
                "c := 3;    // glued\n");
        }

        [Fact]
        public void TrailingComment_AfterEndIf_KeepsTheSameGap()
        {
            AssertLayout(
                "IF x THEN\n" +
                "    y := 1;\n" +
                "END_IF // note\n",
                "IF x THEN\n" +
                "    y := 1;\n" +
                "END_IF    // note\n");
        }

        [Fact]
        public void TrailingComment_InVarBlock_KeepsTheSameGap()
        {
            AssertLayout(
                "PROGRAM P\n" +
                "VAR\n" +
                "    x : INT; // note\n" +
                "    y : INT;//glued\n" +
                "END_VAR\n" +
                "END_PROGRAM\n",
                "PROGRAM P\n" +
                "VAR\n" +
                "    x : INT;    // note\n" +
                "    y : INT;    // glued\n" +
                "END_VAR\n" +
                "END_PROGRAM\n");
        }

        [Fact]
        public void TrailingComment_OnStructMember_KeepsTheSameGap()
        {
            AssertLayout(
                "TYPE T :\n" +
                "STRUCT\n" +
                "    a : INT; // note\n" +
                "END_STRUCT\n" +
                "END_TYPE\n",
                "TYPE T :\n" +
                "STRUCT\n" +
                "    a : INT;    // note\n" +
                "END_STRUCT\n" +
                "END_TYPE\n");
        }

        [Fact]
        public void TrailingComment_MidStatement_KeepsTheSameGap()
        {
            // A statement whose middle token ends its source line is written
            // token-for-token, so the gap comes from the verbatim path rather than
            // from WriteTrailingTrivia — it has to be the same 4 spaces.
            AssertLayout(
                "fb.Execute(a := 1, // note\n" +
                "b := 2);\n",
                "fb.Execute(a := 1,    // note\n" +
                "b := 2);\n");
        }

        [Fact]
        public void TrailingComment_AfterAPragma_KeepsTheSameGap()
        {
            AssertLayout(
                "{attribute 'qualified_only'} // note\n" +
                "a := 1;\n",
                "{attribute 'qualified_only'}    // note\n" +
                "a := 1;\n");
        }

        [Fact]
        public void BlockComment_KeepsASingleSpace_AndItsOwnContent()
        {
            // The 4-space gap and the '// ' normalization are about the marker a
            // reader scans for. A block comment keeps one space and its text.
            AssertLayout(
                "a := f(x, (* note *) y);\n" +
                "b := 1;(* tail *)\n",
                "a := f(x, (* note *) y);\n" +
                "b := 1; (* tail *)\n");
        }

        // ---- rule 3: a standalone comment takes the indent of the line below ----

        [Fact]
        public void StandaloneComment_TakesTheIndentOfTheLineBelow()
        {
            AssertLayout(
                "IF x THEN\n" +
                "// head\n" +
                "y := 1;\n" +
                "END_IF\n",
                "IF x THEN\n" +
                "    // head\n" +
                "    y := 1;\n" +
                "END_IF\n");
        }

        [Fact]
        public void StandaloneComment_BeforeEndIf_FollowsEndIfsColumn()
        {
            // The line below the comment is END_IF, back at column 0, so the
            // comment goes there too — the rule follows the next line wherever it
            // lands, it does not remember the block the comment was written in.
            AssertLayout(
                "IF x THEN\n" +
                "    y := 1;\n" +
                "    // tail\n" +
                "END_IF\n",
                "IF x THEN\n" +
                "    y := 1;\n" +
                "// tail\n" +
                "END_IF\n");
        }

        [Fact]
        public void StandaloneComment_InVarBlock_TakesTheMembersIndent()
        {
            AssertLayout(
                "PROGRAM P\n" +
                "VAR\n" +
                "// own line\n" +
                "z : INT;\n" +
                "END_VAR\n" +
                "END_PROGRAM\n",
                "PROGRAM P\n" +
                "VAR\n" +
                "    // own line\n" +
                "    z : INT;\n" +
                "END_VAR\n" +
                "END_PROGRAM\n");
        }

        // ---- comments must survive ----

        [Fact]
        public void CommentOnTheLastLineOfTheBody_IsNotDeleted()
        {
            // The source ends on the same line as the comment, so the lexer reaches
            // end-of-file while the comment is still pending. It used to be filed as
            // EOF's leading trivia and thrown away with it.
            AssertLayout(
                "a := 1;\n" +
                "b := 2; // last line\n",
                "a := 1;\n" +
                "b := 2;    // last line\n");
        }

        [Fact]
        public void CommentLinesAfterTheLastStatement_AreNotDeleted()
        {
            AssertLayout(
                "a := 1;\n" +
                "// tail one\n" +
                "// tail two\n",
                "a := 1;\n" +
                "// tail one\n" +
                "// tail two\n");
        }

        [Fact]
        public void BodyThatIsNothingButComments_IsNotDeleted()
        {
            // A POU body the author disabled wholesale is still the author's text.
            AssertLayout(
                "//a := 1;\n" +
                "//b := 2;\n",
                "// a := 1;\n" +
                "// b := 2;\n");
        }

        [Fact]
        public void CommentOnACaseLabel_IsNotDeleted()
        {
            AssertLayout(
                "CASE x OF\n" +
                "    1: // note\n" +
                "        y := 1;\n" +
                "    // own line\n" +
                "    2:\n" +
                "        y := 2;\n" +
                "END_CASE\n",
                "CASE x OF\n" +
                "    1:    // note\n" +
                "        y := 1;\n" +
                "    // own line\n" +
                "    2:\n" +
                "        y := 2;\n" +
                "END_CASE\n");
        }

        [Fact]
        public void CommentedOutBody_OfAFullPou_IsNotDeleted()
        {
            // The comment lines sit directly above END_PROGRAM, so rule 3 puts them
            // at END_PROGRAM's column — the same as a comment above END_IF.
            AssertLayout(
                "PROGRAM P\n" +
                "VAR\n" +
                "    x : INT;\n" +
                "END_VAR\n" +
                "//x := 1;\n" +
                "//x := 2;\n" +
                "END_PROGRAM\n",
                "PROGRAM P\n" +
                "VAR\n" +
                "    x : INT;\n" +
                "END_VAR\n" +
                "// x := 1;\n" +
                "// x := 2;\n" +
                "END_PROGRAM\n");
        }

        // ---- the reported file ----

        [Fact]
        public void ReportedCallArguments_AllShareTheColumnOfTheFirstOne()
        {
            // As reported: the first argument at 4 spaces, every following one at 5,
            // and one argument padded to 'cbParameter  := 0'. NeedsSpaceBefore only
            // knows the previous token, so the comma's space used to leak onto the
            // start of every continuation line.
            AssertLayout(
                "IF cmdQuery.ExecuteDataReturn(\n" +
                "            hDBID := dbId,\n" +
                "             pExpression := ADR(sqlCmd),\n" +
                "             cbExpression := SIZEOF(sqlCmd),\n" +
                "             pData := 0,\n" +
                "             cbData := 0,\n" +
                "             pParameter := 0,\n" +
                "             cbParameter  := 0,\n" +
                "             nStartIndex := 0) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n",
                "IF cmdQuery.ExecuteDataReturn(\n" +
                "    hDBID := dbId,\n" +
                "    pExpression := ADR(sqlCmd),\n" +
                "    cbExpression := SIZEOF(sqlCmd),\n" +
                "    pData := 0,\n" +
                "    cbData := 0,\n" +
                "    pParameter := 0,\n" +
                "    cbParameter := 0,\n" +
                "    nStartIndex := 0) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n");
        }

        [Fact]
        public void ReportedCommentedArguments_LoseTheirPaddingAndAlignWithTheLiveOnes()
        {
            // The same call after a run of the version that padded comments and
            // drifted every continuation line one column right.
            AssertLayout(
                "IF cmdQuery.ExecuteDataReturn(\n" +
                "             hDBID := dbId,\n" +
                "             //             pData := ADR(search),\n" +
                "\n" +
                "             //             cbData := SIZEOF(search),\n" +
                "             nStartIndex := 0) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n",
                "IF cmdQuery.ExecuteDataReturn(\n" +
                "    hDBID := dbId,\n" +
                "    // pData := ADR(search),\n" +
                "\n" +
                "    // cbData := SIZEOF(search),\n" +
                "    nStartIndex := 0) THEN\n" +
                "    x := 1;\n" +
                "END_IF\n");
        }
    }
}
