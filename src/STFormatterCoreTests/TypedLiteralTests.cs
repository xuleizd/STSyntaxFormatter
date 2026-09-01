using STFormatterCore.Configuration;
using STFormatterCore.Formatter;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using Xunit;

namespace STFormatterCoreTests
{
    /// <summary>
    /// IEC 61131-3 syntax coverage: typed literals, enum value access via '#',
    /// and doubled-quote escapes inside string literals must survive formatting
    /// byte-for-byte — splitting any of them produces code that no longer
    /// compiles in TwinCAT.
    /// </summary>
    public class TypedLiteralTests
    {
        private static string Format(string source)
        {
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            return new STFormatter(new FormatterOptions()).Format(cst, source);
        }

        [Theory]
        [InlineData("INT#5")]
        [InlineData("WORD#16#FF")]
        [InlineData("BOOL#TRUE")]
        [InlineData("REAL#3.14")]
        [InlineData("BYTE#2#1010")]
        [InlineData("INT#-5")]
        public void TypedLiteral_OnStandardType_StaysGlued(string literal)
        {
            var result = Format("PROGRAM p\nVAR\n\tx : INT;\nEND_VAR\nx := " + literal + ";\nEND_PROGRAM");
            Assert.Contains(literal, result);
        }

        [Theory]
        [InlineData("T#1s")]
        [InlineData("TIME#1s500ms")]
        [InlineData("D#2023-01-01")]
        [InlineData("DT#1998-12-07-12:00:00")]
        [InlineData("TOD#12:00:00.123")]
        [InlineData("LTIME#2d")]
        public void TypedLiteral_TimeAndDate_StaysGlued(string literal)
        {
            var result = Format("PROGRAM p\nVAR\n\tx : INT;\nEND_VAR\nx := " + literal + ";\nEND_PROGRAM");
            Assert.Contains(literal, result);
        }

        [Fact]
        public void EnumValueAccess_StaysGlued()
        {
            var result = Format("PROGRAM p\nVAR\n\tm : E_Mode;\nEND_VAR\nm := E_Mode#Running;\nEND_PROGRAM");
            Assert.Contains("E_Mode#Running", result);
        }

        [Fact]
        public void TypedLiteral_InCallArguments_StaysGlued()
        {
            var result = Format("PROGRAM p\nVAR\n\tx : WORD;\nEND_VAR\nx := LIMIT(WORD#16#00, x, WORD#16#FF);\nEND_PROGRAM");
            Assert.Contains("WORD#16#00", result);
            Assert.Contains("WORD#16#FF", result);
        }

        [Fact]
        public void DoubledSingleQuote_InString_Preserved()
        {
            var result = Format("PROGRAM p\nVAR\n\ts : STRING;\nEND_VAR\ns := 'It''s ok';\nEND_PROGRAM");
            Assert.Contains("'It''s ok'", result);
        }

        [Fact]
        public void DoubledDoubleQuote_InWString_Preserved()
        {
            var result = Format("PROGRAM p\nVAR\n\ts : WSTRING;\nEND_VAR\ns := \"say \"\"hi\"\"\";\nEND_PROGRAM");
            Assert.Contains("\"say \"\"hi\"\"\"", result);
        }

        [Fact]
        public void DollarEscapes_InString_Preserved()
        {
            var result = Format("PROGRAM p\nVAR\n\ts : STRING;\nEND_VAR\ns := 'a$Lb$Tc$$d';\nEND_PROGRAM");
            Assert.Contains("'a$Lb$Tc$$d'", result);
        }
    }
}
