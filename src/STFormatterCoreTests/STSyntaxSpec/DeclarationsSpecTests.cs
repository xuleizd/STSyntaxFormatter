using FluentAssertions;
using Xunit;

namespace STFormatterCoreTests.STSyntaxSpec
{
    /// <summary>
    /// 声明全家族（手册 16.2）：VAR 变体、修饰符组合、AT、访问修饰、SUPER/THIS。
    /// </summary>
    public class DeclarationsSpecTests : SpecBase
    {
        private static void AssertVarBlock(string body, params string[] fragments) =>
            AssertConstruct($"PROGRAM P\n{body}\nEND_PROGRAM", fragments);

        [Theory]
        [InlineData("VAR nVar1 : INT; END_VAR")]
        [InlineData("VAR_INPUT nIn1 : INT; END_VAR")]
        [InlineData("VAR_OUTPUT nOut1 : INT; END_VAR")]
        [InlineData("VAR_IN_OUT bInOut : BOOL; END_VAR")]
        [InlineData("VAR_IN_OUT CONSTANT cReadOnly : STRING(16); END_VAR")]
        [InlineData("VAR_GLOBAL nVarGlob1 : INT; END_VAR")]
        [InlineData("VAR_TEMP nVarTmp1 : INT; END_VAR")]
        [InlineData("VAR_STAT nVarStat1 : INT; END_VAR")]
        [InlineData("VAR_EXTERNAL nVarExt1 : INT; END_VAR")]
        [InlineData("VAR_INST nLast : INT := 0; END_VAR")]
        [InlineData("VAR CONSTANT cTaxFactor : REAL := 1.19; END_VAR")]
        [InlineData("VAR_GLOBAL CONSTANT cMax : INT := 100; END_VAR")]
        [InlineData("VAR RETAIN nRem1 : INT; END_VAR")]
        [InlineData("VAR_GLOBAL RETAIN nRem2 : INT; END_VAR")]
        [InlineData("VAR_GLOBAL PERSISTENT nPers1 : DINT; END_VAR")]
        [InlineData("VAR_OUTPUT RETAIN nX : INT; END_VAR")]
        [InlineData("VAR VAR_INST_STYLE : INT; END_VAR")]
        public void VarBlockVariants(string body)
        {
            AssertVarBlock(body, body.Split(' ')[0]); // VAR 关键词保留
        }

        [Theory]
        [InlineData("PUBLIC nPublic : INT;", "PUBLIC nPublic")]
        [InlineData("PRIVATE nPrivate : INT;", "PRIVATE nPrivate")]
        [InlineData("PROTECTED nProtected : INT;", "PROTECTED nProtected")]
        [InlineData("INTERNAL nInternal : INT;", "INTERNAL nInternal")]
        public void MemberAccessModifiers(string declaration, string fragment)
        {
            AssertVarBlock($"VAR\n{declaration}\nEND_VAR", fragment);
        }

        [Theory]
        [InlineData("a, b, c : INT;", "a, b, c")]
        [InlineData("index, lower, upper : DINT;", "index, lower, upper")]
        public void CommaMultiDeclaration(string declaration, string fragment)
        {
            AssertVarBlock($"VAR\n{declaration}\nEND_VAR", fragment);
        }

        [Fact]
        public void VarInstInMethod()
        {
            AssertConstruct(
                "METHOD Last : INT\nVAR_INST nLast : INT := 0; END_VAR\nLast := nLast;\nEND_METHOD",
                "VAR_INST", "nLast");
        }

        [Fact]
        public void SuperAndThis_StatementLevel_Floor()
        {
            // 语句开头的 THIS^./SUPER^. 走 recovery——底线是等价性。
            AssertEquivalenceFloor("SUPER^();", "SUPER^()");
            AssertEquivalenceFloor("SUPER^.METH_DoIt();", "SUPER^.METH_DoIt()");
            AssertEquivalenceFloor("nBase := SUPER^.nCnt;", "SUPER^.nCnt");
            AssertEquivalenceFloor("THIS^.nVarB := 222;", "THIS^.nVarB := 222");
        }

        [Fact]
        public void DotPrefixedGlobal_Floor()
        {
            AssertEquivalenceFloor("n := .nGlobVar1;", ".nGlobVar1");
        }

        [Fact]
        public void ReferenceBindingOnFbInput()
        {
            AssertEquivalenceFloor("fbSample.refInput1 REF= n1;", "REF=");
        }

        [Fact]
        public void DoubleAtAddressBinding()
        {
            // TwinCAT 导出文件里出现过的 "AT AT%I*" 形态。
            AssertVarBlock("VAR\nbSensor AT AT%I* : BOOL;\nEND_VAR", "AT%I*");
        }
    }
}
