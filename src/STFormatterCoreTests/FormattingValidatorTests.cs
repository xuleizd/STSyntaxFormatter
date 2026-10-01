using STFormatterCore.Configuration;
using STFormatterCore.Formatter;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Validation;
using Xunit;

namespace STFormatterCoreTests
{
    /// <summary>
    /// FormattingValidator is the machine safety net that makes "the formatter
    /// silently corrupted my code" impossible: formatting may only change
    /// whitespace, line breaks and keyword/type-name casing. Every case here
    /// either proves a legitimate change passes or proves a corrupting change is
    /// caught — the two real-world corruptions (REPEAT swallowing statements,
    /// STRUCT RETAIN being deleted) are pinned by reconstructing their exact
    /// buggy outputs from the 1.8.6 / 1.8.7 reports.
    /// </summary>
    public class FormattingValidatorTests
    {
        private static string Format(string source)
        {
            var options = new FormatterOptions { LineEnding = LineEnding.LF };
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            return new STFormatter(options).Format(cst, source);
        }

        #region Legitimate changes must pass

        [Fact]
        public void NormalFormatting_Passes()
        {
            var source = "program Main\nvar x : int; end_var\nif x>1 then x:=x-1; end_if\nend_program";
            Assert.True(FormattingValidator.Validate(source, Format(source)).IsValid);
        }

        [Fact]
        public void KeywordUppercasing_Passes()
        {
            // Keywords are uppercased, and TypeCase may change identifier casing —
            // both compare case-insensitively.
            var source = "program p\nvar a : int; end_var\nend_program";
            var formatted = "PROGRAM p\r\nVAR\r\n    a : INT;\r\nEND_VAR\r\nEND_PROGRAM\r\n";
            Assert.True(FormattingValidator.Validate(source, formatted).IsValid);
        }

        [Fact]
        public void CommentGapNormalization_Passes()
        {
            // '//x' → '// x' only adjusts the gap; content must survive.
            var source = "x := 1; //note\n";
            var formatted = "x := 1;    // note\n";
            Assert.True(FormattingValidator.Validate(source, formatted).IsValid);
        }

        [Fact]
        public void UnknownDebrisNormalizedAway_Passes()
        {
            // A stray ';' parks in an UnknownNode and is normalized away by
            // design — that must not trip the validator.
            var source = "x := 1;;\ny := 2;\n";
            Assert.True(FormattingValidator.Validate(source, Format(source)).IsValid);
        }

        [Fact]
        public void EmptyOutput_Fails()
        {
            var result = FormattingValidator.Validate("x := 1;", "");
            Assert.False(result.IsValid);
            Assert.Contains("空", result.FailureMessage);
        }

        #endregion

        #region Historical bugs must be caught

        [Fact]
        public void StructRetainDeleted_1_8_7_Output_Fails()
        {
            // The exact shape of the 1.8.7 bug: formatting "STRUCT RETAIN" deleted
            // the RETAIN token, silently dropping the retain property.
            var source = "TYPE ST_R : STRUCT RETAIN\nnValue : DINT;\nEND_STRUCT\nEND_TYPE\n";
            var corrupt = "TYPE ST_R :\nSTRUCT\n    nValue : DINT;\nEND_STRUCT\nEND_TYPE\n";
            var result = FormattingValidator.Validate(source, corrupt);
            Assert.False(result.IsValid);
            Assert.Contains("token", result.FailureMessage);
        }

        [Fact]
        public void RepeatUntilSwallowsStatements_1_8_6_Output_Fails()
        {
            // The exact shape of the 1.8.6 bug: the UNTIL condition collected past
            // END_REPEAT, the following statements ended up inside the condition,
            // END_REPEAT moved after them and a bogus ';' was appended.
            var source = "REPEAT\n    a := a - 1;\nUNTIL a <= 0\nEND_REPEAT\n\nIF a > 0 THEN\n    a := 1;\nEND_IF\n";
            var corrupt = "REPEAT\r\n    a := a - 1;\r\nUNTIL a <= 0 IF a > 0 THEN\r\n    a := 1;\r\nEND_IF\r\nEND_REPEAT;\r\n";
            var result = FormattingValidator.Validate(source, corrupt);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void StatementMovedIntoLoopBody_Fails()
        {
            // Structure-level corruption with the token order preserved: y := 2
            // moved from outside the loop into it. The tree-shape comparison
            // catches this even though no token is added or dropped.
            var source = "WHILE a > 0 DO\na := a - 1;\nEND_WHILE\ny := 2;\n";
            var corrupt = "WHILE a > 0 DO\na := a - 1;\ny := 2;\nEND_WHILE\n";
            var result = FormattingValidator.Validate(source, corrupt);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Token-level corruptions

        [Fact]
        public void InventedToken_Fails()
        {
            var result = FormattingValidator.Validate("x := 1;\n", "x := 1;\nx := 2;\n");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ReorderedTokens_Fails()
        {
            var result = FormattingValidator.Validate("x := 1;\ny := 2;\n", "y := 2;\nx := 1;\n");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void StringLiteralChanged_Fails()
        {
            // String literals survive verbatim — even case changes are refused.
            var result = FormattingValidator.Validate("s := 'Abc';\n", "s := 'abc';\n");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void NumericLiteralChanged_Fails()
        {
            var result = FormattingValidator.Validate("x := 10;\n", "x := 100;\n");
            Assert.False(result.IsValid);
        }

        #endregion

        #region Comment corruptions

        [Fact]
        public void DeletedComment_Fails()
        {
            var result = FormattingValidator.Validate("x := 1; // note\n", "x := 1;\n");
            Assert.False(result.IsValid);
            Assert.Contains("注释", result.FailureMessage);
        }

        [Fact]
        public void MalformedInput_CommentLossOnDroppedTokens_Fails()
        {
            // The _Safety fixture shape: comments ride as trivia on malformed
            // tokens the parser never files into any node. A tree-walking
            // comparison cannot see them — whatever the first failing check
            // reports, the corruption must not pass validation.
            var source = "VAR :\nErr_Ack AT %;\nBOOL;; // error ack\nRestart AT %;\nBOOL;; // restart\nEND_VAR\n";
            var corrupt = "VAR\nEND_VAR\n";
            var result = FormattingValidator.Validate(source, corrupt);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void MalformedInput_CommentsOnlyDifference_Fails()
        {
            // Both sides keep every code token (all debris parks in UnknownNode
            // and is exempt), the ONLY difference is two comments hanging on
            // tokens the parser drops entirely — the full-token-list multiset
            // must catch it.
            var source = "VAR :\n%%%; // first\n%%%; // second\nEND_VAR\n";
            var corrupt = "VAR :\n%%%;\n%%%;\nEND_VAR\n";
            var result = FormattingValidator.Validate(source, corrupt);
            Assert.False(result.IsValid);
            Assert.Contains("注释", result.FailureMessage);
        }

        [Fact]
        public void DuplicatedComment_Fails()
        {
            // Same tokens, but the comment now appears twice — the multiset
            // comparison catches pure duplication.
            var result = FormattingValidator.Validate("x := 1; // note\n", "x := 1;    // note    (* note *)\n");
            Assert.False(result.IsValid);
            Assert.Contains("注释", result.FailureMessage);
        }

        [Fact]
        public void CommentContentChanged_Fails()
        {
            var result = FormattingValidator.Validate("x := 1; // note\n", "x := 1;    // notes\n");
            Assert.False(result.IsValid);
        }

        #endregion
    }
}
