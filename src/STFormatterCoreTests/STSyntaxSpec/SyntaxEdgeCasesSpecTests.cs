using FluentAssertions;
using Xunit;

namespace STFormatterCoreTests.STSyntaxSpec
{
    /// <summary>
    /// 已知不支持构造的底线套件（docs/st-syntax-reference.md 第 7 节清单）：
    /// 排版可以劣化，但绝不许丢 token / 丢注释 / 破坏可重解析性——
    /// 这是等价性校验器守护的最后防线。每一个条目都对应手册中的真实语法。
    /// </summary>
    public class SyntaxEdgeCasesSpecTests : SpecBase
    {
        [Fact]
        public void GenericFb_DeclarationPosition_Floor()
        {
            AssertEquivalenceFloor(
                "FUNCTION_BLOCK FB_Sample\nVAR\nfbSample1 : FB_Sample<100>;\nfbSample2 : FB_Sample<(2 * cConst)>;\nEND_VAR\nEND_FUNCTION_BLOCK",
                "FB_Sample<100>", "FB_Sample<(2 * cConst)>");
        }

        [Fact]
        public void GenericExtends_Floor()
        {
            AssertEquivalenceFloor(
                "FUNCTION_BLOCK FB_Sub EXTENDS FB_Sample<100>\nVAR\nn : INT;\nEND_VAR\nEND_FUNCTION_BLOCK",
                "EXTENDS FB_Sample<100>");
        }

        [Fact]
        public void VarGenericConstant_Floor()
        {
            AssertEquivalenceFloor(
                "FUNCTION_BLOCK FB_Sample\nVAR_GENERIC CONSTANT nMaxLen : UDINT := 1;\nEND_VAR\nEND_FUNCTION_BLOCK",
                "VAR_GENERIC CONSTANT", "nMaxLen");
        }

        [Fact]
        public void LdateAndTime_FullPrefix_Floor()
        {
            AssertEquivalenceFloor(
                "VAR\nx : LDATE_AND_TIME := LDATE_AND_TIME#1996-05-06-15:36:30;\nEND_VAR",
                "LDATE_AND_TIME#1996-05-06-15:36:30");
        }

        [Fact]
        public void LtimeOfDay_FullPrefix_Floor()
        {
            AssertEquivalenceFloor(
                "VAR\nt : LTIME_OF_DAY := LTIME_OF_DAY#15:36:30.123;\nEND_VAR",
                "LTIME_OF_DAY#15:36:30.123");
        }

        [Fact]
        public void UnsupportedConstructs_StillKeepTheirComments()
        {
            // 注释挂在任何构造上都不许丢——包括不支持的构造。
            string source =
                "VAR_GENERIC CONSTANT nMaxLen : UDINT := 1; // generic limit\n" +
                "// second comment\n";
            string formatted = Format(source);
            formatted.Should().Contain("// generic limit");
            formatted.Should().Contain("// second comment");
        }

        [Fact]
        public void TryCatch_KeepsCommentInsideBlocks()
        {
            string source =
                "__TRY\n// inside try\npSample^ := TRUE;\n__CATCH(exc)\n// inside catch\nnDivisor := 1;\n__ENDTRY";
            string formatted = Format(source);
            formatted.Should().Contain("// inside try");
            formatted.Should().Contain("// inside catch");
        }

        [Fact]
        public void TypoSemicolons_AreNormalizedAwayButNothingElseChanges()
        {
            // `;;` 属于 UnknownNode 垃圾，规范化是既定行为——其余 token 一个不少。
            string source = "x := 1;;\ny := 2;\n";
            AssertConstruct(source, "x := 1;", "y := 2;");
        }
    }
}
