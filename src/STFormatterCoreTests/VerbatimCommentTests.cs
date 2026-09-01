using Xunit;
using Xunit.Abstractions;
using STFormatterCore.Configuration;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Formatter;

namespace STFormatterCoreTests
{
    /// <summary>
    /// Statements that carry a comment mid-statement (a trailing comment on a
    /// token that is NOT the last one) are preserved token-for-token instead of
    /// being reflowed. These tests pin the verbatim state machine that replaced
    /// the destructive WriteTriviaVerbatim path: intra-line whitespace must
    /// never be dropped, separate source lines must never be merged, and with
    /// KeepEmptyLines=false only blank lines may disappear.
    /// </summary>
    public class VerbatimCommentTests
    {
        private readonly ITestOutputHelper _out;
        public VerbatimCommentTests(ITestOutputHelper o) { _out = o; }

        private static string Format(string source, bool keepEmptyLines)
        {
            var options = new FormatterOptions { KeepEmptyLines = keepEmptyLines, LineEnding = LineEnding.LF };
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            return new STFormatter(options).Format(cst, source);
        }

        [Fact]
        public void MidStatementTrailingComment_MultiLineCall_NoKeep()
        {
            var source =
                "PROGRAM P\n" +
                "fb.Execute(a := 1, // note\n" +
                "b := 2);\n" +
                "END_PROGRAM";
            var result = Format(source, keepEmptyLines: false);
            _out.WriteLine(result);
            // Corruption signature: the trailing comment swallows the following
            // tokens because the newline after it was dropped.
            Assert.DoesNotMatch(@"//[^\r\n]*\bb\s*:=\s*2", result);
            Assert.Contains("// note", result);
            Assert.Contains("b := 2", result);
        }

        [Fact]
        public void MidStatementTrailingComment_MultiLineCall_Keep()
        {
            var source =
                "PROGRAM P\n" +
                "fb.Execute(a := 1, // note\n" +
                "b := 2);\n" +
                "END_PROGRAM";
            var result = Format(source, keepEmptyLines: true);
            _out.WriteLine(result);
            Assert.Contains("// note", result);
            Assert.Contains("b := 2", result);
        }

        [Fact]
        public void TwoStatementsOnOneLineTrailingComment_NoKeep()
        {
            var source =
                "PROGRAM P\n" +
                "a := 1; // note\n" +
                "b := 2;\n" +
                "END_PROGRAM";
            var result = Format(source, keepEmptyLines: false);
            _out.WriteLine(result);
            Assert.Contains("// note", result);
            Assert.Contains("b := 2;", result);
        }

        [Fact]
        public void MidStatementMultiLineComment_BothModes()
        {
            var source =
                "PROGRAM P\n" +
                "a := f(x, (* note *) y);\n" +
                "END_PROGRAM";
            foreach (var keep in new[] { false, true })
            {
                var result = Format(source, keep);
                _out.WriteLine($"KeepEmptyLines={keep}:");
                _out.WriteLine(result);
                Assert.Contains("(* note *)", result);
                Assert.Contains("y", result);
            }
        }
    }
}
