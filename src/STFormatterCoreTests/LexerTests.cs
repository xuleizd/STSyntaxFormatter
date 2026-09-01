using System.Collections.Generic;
using System.Linq;
using STFormatterCore.Lexer;
using Xunit;

namespace STFormatterCoreTests
{
    public class LexerTests
    {
        #region Helpers

        private List<Token> Tokenize(string source)
        {
            return new STLexer(source).Tokenize();
        }

        /// <summary>Returns all tokens except the final EndOfFile.</summary>
        private List<Token> TokenizeNoEof(string source)
        {
            var tokens = Tokenize(source);
            tokens.RemoveAt(tokens.Count - 1);
            return tokens;
        }

        #endregion

        #region 1. Keywords – case insensitive

        [Theory]
        [InlineData("PROGRAM", TokenKind.Keyword_Program)]
        [InlineData("program", TokenKind.Keyword_Program)]
        [InlineData("Program", TokenKind.Keyword_Program)]
        [InlineData("IF", TokenKind.Keyword_If)]
        [InlineData("if", TokenKind.Keyword_If)]
        [InlineData("FOR", TokenKind.Keyword_For)]
        [InlineData("for", TokenKind.Keyword_For)]
        [InlineData("END_PROGRAM", TokenKind.Keyword_EndProgram)]
        [InlineData("WHILE", TokenKind.Keyword_While)]
        [InlineData("FUNCTION_BLOCK", TokenKind.Keyword_FunctionBlock)]
        [InlineData("FUNCTION", TokenKind.Keyword_Function)]
        [InlineData("END_FUNCTION", TokenKind.Keyword_EndFunction)]
        [InlineData("END_FUNCTION_BLOCK", TokenKind.Keyword_EndFunctionBlock)]
        [InlineData("VAR", TokenKind.Keyword_Var)]
        [InlineData("END_VAR", TokenKind.Keyword_EndVar)]
        [InlineData("VAR_RETAIN", TokenKind.Keyword_VarRetain)]
        [InlineData("var_retain", TokenKind.Keyword_VarRetain)]
        [InlineData("VAR_PERSISTENT", TokenKind.Keyword_VarPersistent)]
        [InlineData("var_persistent", TokenKind.Keyword_VarPersistent)]
        [InlineData("RETURN", TokenKind.Keyword_Return)]
        public void SimpleKeywords_CaseInsensitive(string text, TokenKind expectedKind)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(expectedKind, tokens[0].Kind);
        }

        [Theory]
        [InlineData("CONFIGURATION", TokenKind.Keyword_Configuration)]
        [InlineData("configuration", TokenKind.Keyword_Configuration)]
        [InlineData("END_CONFIGURATION", TokenKind.Keyword_EndConfiguration)]
        [InlineData("RESOURCE", TokenKind.Keyword_Resource)]
        [InlineData("END_RESOURCE", TokenKind.Keyword_EndResource)]
        [InlineData("TASK", TokenKind.Keyword_Task)]
        [InlineData("WITH", TokenKind.Keyword_With)]
        [InlineData("NON_RETAIN", TokenKind.Keyword_NonRetain)]
        [InlineData("GET", TokenKind.Keyword_Get)]
        [InlineData("SET", TokenKind.Keyword_Set)]
        [InlineData("NEW", TokenKind.Keyword_New)]
        [InlineData("DELETE", TokenKind.Keyword_Delete)]
        [InlineData("REF_TO", TokenKind.Keyword_RefTo)]
        [InlineData("END_UNION", TokenKind.Keyword_EndUnion)]
        [InlineData("USING", TokenKind.Keyword_Using)]
        public void NewAndExistingKeywords(string text, TokenKind expectedKind)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(expectedKind, tokens[0].Kind);
        }

        #endregion

        #region 2. Identifiers

        [Theory]
        [InlineData("myVar")]
        [InlineData("_temp")]
        [InlineData("Counter1")]
        [InlineData("ABC_XYZ")]
        public void Identifiers_Recognized(string text)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
            Assert.Equal(text, tokens[0].Text);
        }

        #endregion

        #region 3. String Literals

        [Fact]
        public void StringLiteral_SingleQuoted()
        {
            var tokens = TokenizeNoEof("'hello'");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.StringLiteral, tokens[0].Kind);
            Assert.Equal("'hello'", tokens[0].Text);
        }

        [Fact]
        public void WStringLiteral_DoubleQuoted()
        {
            var tokens = TokenizeNoEof("\"world\"");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.WStringLiteral, tokens[0].Kind);
            Assert.Equal("\"world\"", tokens[0].Text);
        }

        [Theory]
        [InlineData("'hello$Nworld'")]
        [InlineData("'dollar$$sign'")]
        [InlineData("'quote$'inside'")]
        public void StringLiteral_EscapeSequences(string text)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(TokenKind.StringLiteral, tokens[0].Kind);
        }

        #endregion

        #region 4. Numeric Literals

        [Theory]
        [InlineData("42", TokenKind.IntegerLiteral)]
        [InlineData("0", TokenKind.IntegerLiteral)]
        [InlineData("16#FF", TokenKind.IntegerLiteral)]
        [InlineData("2#1010", TokenKind.IntegerLiteral)]
        [InlineData("8#77", TokenKind.IntegerLiteral)]
        public void NumericLiterals_Integer(string text, TokenKind expectedKind)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(expectedKind, tokens[0].Kind);
        }

        [Theory]
        [InlineData("3.14", TokenKind.RealLiteral)]
        [InlineData("1.0E-5", TokenKind.RealLiteral)]
        [InlineData("2.0E+3", TokenKind.RealLiteral)]
        public void NumericLiterals_Real(string text, TokenKind expectedKind)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(expectedKind, tokens[0].Kind);
        }

        #endregion

        #region 5. Typed Literals

        [Theory]
        [InlineData("T#1s")]
        [InlineData("TIME#1s500ms")]
        [InlineData("DATE#2023-01-01")]
        [InlineData("DT#2023-01-01-12:00:00")]
        [InlineData("TOD#12:00:00")]
        public void TypedLiterals(string text)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(TokenKind.TypedLiteral, tokens[0].Kind);
        }

        #endregion

        #region 6. Single-line Comments → Trivia

        [Fact]
        public void SingleLineComment_BecomesTrivia()
        {
            // "x // comment\ny" → comment becomes trivia somewhere in the token stream
            var tokens = Tokenize("x // comment\ny");
            // The comment is trivia attached to some token (either trailing of x or leading of EOF)
            bool foundComment = false;
            foreach (var tok in tokens)
            {
                if (tok.TrailingTrivia.Any(t => t.Kind == TriviaKind.SingleLineComment) ||
                    tok.LeadingTrivia.Any(t => t.Kind == TriviaKind.SingleLineComment))
                {
                    foundComment = true;
                    break;
                }
            }
            Assert.True(foundComment, "Single-line comment should appear as trivia on some token");
        }

        #endregion

        #region 7. Multi-line Comments → Trivia (including nested)

        [Fact]
        public void MultiLineComment_Iec_BecomesTrivia()
        {
            var tokens = Tokenize("(* this is a comment *)\nx");
            // The comment becomes trivia on x (leading)
            var x = tokens[0];
            Assert.Equal(TokenKind.Identifier, x.Kind);
            Assert.Contains(x.LeadingTrivia, t => t.Kind == TriviaKind.MultiLineComment);
        }

        [Fact]
        public void MultiLineComment_Nested()
        {
            var tokens = Tokenize("(* outer (* inner *) still comment *)\nx");
            var x = tokens[0];
            Assert.Equal(TokenKind.Identifier, x.Kind);
            Assert.Contains(x.LeadingTrivia, t => t.Kind == TriviaKind.MultiLineComment);
        }

        [Fact]
        public void MultiLineComment_CStyle()
        {
            var tokens = Tokenize("/* c-style comment */\nx");
            var x = tokens[0];
            Assert.Equal(TokenKind.Identifier, x.Kind);
            Assert.Contains(x.LeadingTrivia, t => t.Kind == TriviaKind.MultiLineComment);
        }

        #endregion

        #region 8. Operators

        [Theory]
        [InlineData(":=", TokenKind.Assign)]
        [InlineData("**", TokenKind.StarStar)]
        [InlineData("<>", TokenKind.NotEqual)]
        [InlineData("<=", TokenKind.LessEqual)]
        [InlineData(">=", TokenKind.GreaterEqual)]
        public void Operators_Symbolic(string text, TokenKind expectedKind)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(expectedKind, tokens[0].Kind);
        }

        [Fact]
        public void RefAssign_Operator()
        {
            var tokens = TokenizeNoEof("REF=");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.RefAssign, tokens[0].Kind);
        }

        #endregion

        #region 9. Pragma / Attribute tokens

        [Fact]
        public void Pragma_AttributeDirective()
        {
            var tokens = TokenizeNoEof("{attribute 'name' := 'value'}");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.Pragma, tokens[0].Kind);
            Assert.Equal("{attribute 'name' := 'value'}", tokens[0].Text);
        }

        [Fact]
        public void Pragma_NestedBraces()
        {
            var tokens = TokenizeNoEof("{attribute 'x' := '{inner}'}");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.Pragma, tokens[0].Kind);
        }

        #endregion

        #region 10. Trivia Attachment

        [Fact]
        public void Trivia_EndOfLineComment_TrailingTrivia()
        {
            // "x := 1; // end-of-line" → comment should be trivia somewhere
            var tokens = Tokenize("x := 1; // end-of-line");
            bool foundComment = false;
            foreach (var tok in tokens)
            {
                foreach (var t in tok.TrailingTrivia)
                    if (t.Kind == TriviaKind.SingleLineComment) foundComment = true;
                foreach (var t in tok.LeadingTrivia)
                    if (t.Kind == TriviaKind.SingleLineComment) foundComment = true;
            }
            Assert.True(foundComment, "End-of-line comment should appear as trivia on some token");
        }

        [Fact]
        public void Trivia_StandaloneComment_LeadingTrivia()
        {
            // "// comment\nx" → comment is leading trivia of x (or EOF if x is last)
            var tokens = Tokenize("// comment\nx");
            bool foundComment = false;
            foreach (var tok in tokens)
            {
                if (tok.LeadingTrivia.Any(t => t.Kind == TriviaKind.SingleLineComment))
                {
                    foundComment = true;
                    break;
                }
            }
            Assert.True(foundComment, "Standalone comment should be leading trivia on some token");
        }

        #endregion

        #region 11. Empty Input

        [Fact]
        public void EmptyInput_ProducesOnlyEndOfFile()
        {
            var tokens = Tokenize("");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.EndOfFile, tokens[0].Kind);
        }

        [Fact]
        public void WhitespaceOnly_ProducesOnlyEndOfFile()
        {
            var tokens = Tokenize("   \t  ");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.EndOfFile, tokens[0].Kind);
        }

        #endregion

        #region 12. Bad / Unknown Characters

        [Fact]
        public void BadCharacter_ProducesBadToken()
        {
            var tokens = TokenizeNoEof("@");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.BadToken, tokens[0].Kind);
        }

        [Fact]
        public void BadCharacter_MixedWithValid()
        {
            // "@x" → BadToken, Identifier
            var tokens = TokenizeNoEof("@x");
            Assert.Equal(2, tokens.Count);
            Assert.Equal(TokenKind.BadToken, tokens[0].Kind);
            Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
        }

        #endregion

        #region 13. Direct Addresses

        [Theory]
        [InlineData("%IX0.0")]
        [InlineData("%QX0.1")]
        [InlineData("%MW10")]
        [InlineData("%MX0.0")]
        [InlineData("%I*")]
        [InlineData("%QX*")]
        [InlineData("%IB0")]
        [InlineData("%QW100")]
        [InlineData("%MD50")]
        public void DirectAddress_Recognized(string text)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(TokenKind.DirectAddress, tokens[0].Kind);
            Assert.Equal(text, tokens[0].Text);
        }

        #endregion

        #region 14. Additional Keywords

        [Theory]
        [InlineData("AND", TokenKind.Keyword_And)]
        [InlineData("OR", TokenKind.Keyword_Or)]
        [InlineData("XOR", TokenKind.Keyword_Xor)]
        [InlineData("NOT", TokenKind.Keyword_Not)]
        [InlineData("MOD", TokenKind.Keyword_Mod)]
        [InlineData("AND_THEN", TokenKind.Keyword_AndThen)]
        [InlineData("OR_ELSE", TokenKind.Keyword_OrElse)]
        [InlineData("EXPT", TokenKind.Keyword_Expt)]
        [InlineData("ABSTRACT", TokenKind.Keyword_Abstract)]
        [InlineData("FINAL", TokenKind.Keyword_Final)]
        [InlineData("OVERRIDE", TokenKind.Keyword_Override)]
        [InlineData("PUBLIC", TokenKind.Keyword_Public)]
        [InlineData("PRIVATE", TokenKind.Keyword_Private)]
        [InlineData("PROTECTED", TokenKind.Keyword_Protected)]
        [InlineData("INTERNAL", TokenKind.Keyword_Internal)]
        [InlineData("THIS", TokenKind.Keyword_This)]
        [InlineData("SUPER", TokenKind.Keyword_Super)]
        [InlineData("NAMESPACE", TokenKind.Keyword_Namespace)]
        [InlineData("END_NAMESPACE", TokenKind.Keyword_EndNamespace)]
        [InlineData("ACTION", TokenKind.Keyword_Action)]
        [InlineData("TRANSITION", TokenKind.Keyword_Transition)]
        [InlineData("TRUE", TokenKind.Keyword_True)]
        [InlineData("FALSE", TokenKind.Keyword_False)]
        [InlineData("VAR_INPUT", TokenKind.Keyword_VarInput)]
        [InlineData("VAR_OUTPUT", TokenKind.Keyword_VarOutput)]
        [InlineData("VAR_IN_OUT", TokenKind.Keyword_VarInOut)]
        [InlineData("VAR_TEMP", TokenKind.Keyword_VarTemp)]
        [InlineData("VAR_STAT", TokenKind.Keyword_VarStat)]
        [InlineData("VAR_INST", TokenKind.Keyword_VarInst)]
        [InlineData("VAR_GLOBAL", TokenKind.Keyword_VarGlobal)]
        [InlineData("VAR_EXTERNAL", TokenKind.Keyword_VarExternal)]
        [InlineData("CONTINUE", TokenKind.Keyword_Continue)]
        [InlineData("EXIT", TokenKind.Keyword_Exit)]
        [InlineData("EXTENDS", TokenKind.Keyword_Extends)]
        [InlineData("IMPLEMENTS", TokenKind.Keyword_Implements)]
        [InlineData("VAR_CONSTANT", TokenKind.Keyword_VarConstant)]
        public void AdditionalKeywords(string text, TokenKind expectedKind)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(expectedKind, tokens[0].Kind);
        }

        [Theory]
        [InlineData("and", TokenKind.Keyword_And)]
        [InlineData("or", TokenKind.Keyword_Or)]
        [InlineData("xor", TokenKind.Keyword_Xor)]
        [InlineData("not", TokenKind.Keyword_Not)]
        [InlineData("mod", TokenKind.Keyword_Mod)]
        [InlineData("abstract", TokenKind.Keyword_Abstract)]
        [InlineData("public", TokenKind.Keyword_Public)]
        [InlineData("private", TokenKind.Keyword_Private)]
        [InlineData("namespace", TokenKind.Keyword_Namespace)]
        [InlineData("true", TokenKind.Keyword_True)]
        [InlineData("false", TokenKind.Keyword_False)]
        [InlineData("continue", TokenKind.Keyword_Continue)]
        [InlineData("exit", TokenKind.Keyword_Exit)]
        public void AdditionalKeywords_CaseInsensitive(string text, TokenKind expectedKind)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(expectedKind, tokens[0].Kind);
        }

        #endregion

        #region 15. Additional Symbolic Operators

        [Theory]
        [InlineData("+", TokenKind.Plus)]
        [InlineData("-", TokenKind.Minus)]
        [InlineData("*", TokenKind.Star)]
        [InlineData("/", TokenKind.Slash)]
        [InlineData("=>", TokenKind.OutputAssign)]
        [InlineData("=", TokenKind.Equal)]
        [InlineData("<", TokenKind.LessThan)]
        [InlineData(">", TokenKind.GreaterThan)]
        [InlineData("^", TokenKind.Caret)]
        [InlineData("..", TokenKind.DotDot)]
        public void AdditionalOperators(string text, TokenKind expectedKind)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(expectedKind, tokens[0].Kind);
        }

        [Fact]
        public void OutputAssign_IsSingleToken_NotEqualPlusGreater()
        {
            var tokens = TokenizeNoEof("=>");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.OutputAssign, tokens[0].Kind);
            Assert.Equal("=>", tokens[0].Text);
        }

        [Fact]
        public void DotDot_IsSingleToken_NotTwoDots()
        {
            var tokens = TokenizeNoEof("..");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.DotDot, tokens[0].Kind);
            Assert.Equal("..", tokens[0].Text);
        }

        [Fact]
        public void Caret_IsSingleToken()
        {
            var tokens = TokenizeNoEof("^");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.Caret, tokens[0].Kind);
        }

        #endregion

        #region 16. High-Precision Typed Literals

        [Theory]
        [InlineData("LT#1s")]
        [InlineData("LTIME#1s500ms")]
        [InlineData("LD#2023-01-01")]
        [InlineData("LDATE#2023-01-01")]
        [InlineData("LDT#2023-01-01-12:00:00")]
        [InlineData("LTOD#12:00:00")]
        [InlineData("TIME_OF_DAY#12:00:00")]
        [InlineData("DATE_AND_TIME#2023-01-01-12:00:00")]
        public void HighPrecisionTypedLiterals(string text)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(TokenKind.TypedLiteral, tokens[0].Kind);
        }

        #endregion

        #region 17. More String Escape Sequences

        [Theory]
        [InlineData("'line$Rreturn'")]
        [InlineData("'line$Lfeed'")]
        [InlineData("'form$Pfeed'")]
        [InlineData("'tab$Tchar'")]
        [InlineData("'hex$41char'")]
        [InlineData("'dollar$$sign'")]
        public void StringLiteral_MoreEscapeSequences(string text)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(TokenKind.StringLiteral, tokens[0].Kind);
            Assert.Equal(text, tokens[0].Text);
        }

        #endregion

        #region 18. WSTRING Escape Sequences

        [Theory]
        [InlineData("\"line$Nworld\"")]
        [InlineData("\"dollar$$sign\"")]
        [InlineData("\"tab$Tchar\"")]
        [InlineData("\"line$Rreturn\"")]
        [InlineData("\"line$Lfeed\"")]
        [InlineData("\"hex$41char\"")]
        public void WStringLiteral_EscapeSequences(string text)
        {
            var tokens = TokenizeNoEof(text);
            Assert.Single(tokens);
            Assert.Equal(TokenKind.WStringLiteral, tokens[0].Kind);
            Assert.Equal(text, tokens[0].Text);
        }

        #endregion

        #region 19. Unterminated Strings

        [Fact]
        public void StringLiteral_Unterminated_AtEndOfInput()
        {
            // String that reaches end of input without closing quote
            var tokens = TokenizeNoEof("'unterminated");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.StringLiteral, tokens[0].Kind);
            Assert.Equal("'unterminated", tokens[0].Text);
        }

        [Fact]
        public void WStringLiteral_Unterminated_AtEndOfInput()
        {
            var tokens = TokenizeNoEof("\"unterminated");
            Assert.Single(tokens);
            Assert.Equal(TokenKind.WStringLiteral, tokens[0].Kind);
            Assert.Equal("\"unterminated", tokens[0].Text);
        }

        [Fact]
        public void StringLiteral_Unterminated_AtNewline()
        {
            // String that hits a newline before closing quote
            var tokens = Tokenize("'unterminated\nnextline");
            // Should get: StringLiteral('unterminated), Identifier(nextline), EOF
            var nonEof = tokens.Where(t => t.Kind != TokenKind.EndOfFile).ToList();
            Assert.Equal(2, nonEof.Count);
            Assert.Equal(TokenKind.StringLiteral, nonEof[0].Kind);
            Assert.Equal(TokenKind.Identifier, nonEof[1].Kind);
        }

        #endregion
    }
}
