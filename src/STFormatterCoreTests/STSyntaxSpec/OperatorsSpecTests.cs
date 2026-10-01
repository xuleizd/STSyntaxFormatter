using FluentAssertions;
using Xunit;

namespace STFormatterCoreTests.STSyntaxSpec
{
    /// <summary>
    /// 运算符全家族（手册 16.3）：算术/逻辑/比较/移位/选择/转换/地址/特殊 __ 系。
    /// </summary>
    public class OperatorsSpecTests : SpecBase
    {
        [Theory]
        [InlineData("n := 7 + 2 - 4;", "+")]
        [InlineData("n := 7 * 2 / 4;", "*")]
        [InlineData("n := 9 MOD 2;", "MOD")]
        [InlineData("f := EXPT(7, 2);", "EXPT(7, 2)")]
        [InlineData("n2 := MOVE(n1);", "MOVE(n1)")]
        [InlineData("n := 2#1001_0011 AND 2#1000_1010;", "AND")]
        [InlineData("n := 2#1001_0011 XOR 2#1000_1010;", "XOR")]
        [InlineData("n := NOT 2#1001_0011;", "NOT")]
        [InlineData("n := 2#1001_0011 OR 2#1000_1010;", "OR")]
        public void ArithmeticAndBitwise(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("b := 40 = 40;", "=")]
        [InlineData("b := 40 <> 40;", "<>")]
        [InlineData("b := 20 < 30;", "<")]
        [InlineData("b := 20 > 30;", ">")]
        [InlineData("b := 20 <= 30;", "<=")]
        [InlineData("b := 60 >= 40;", ">=")]
        public void ComparisonOperators(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("n := SHL(nIn, 2);", "SHL(nIn, 2)")]
        [InlineData("n := SHR(nIn, 2);", "SHR(nIn, 2)")]
        [InlineData("n := ROL(nIn, 2);", "ROL(nIn, 2)")]
        [InlineData("n := ROR(nIn, 2);", "ROR(nIn, 2)")]
        [InlineData("n := SEL(TRUE, 3, 4);", "SEL(TRUE, 3, 4)")]
        [InlineData("n := MAX(40, 30, 90);", "MAX(40, 30, 90)")]
        [InlineData("n := MIN(40, 30, 90);", "MIN(40, 30, 90)")]
        [InlineData("n := LIMIT(30, 90, 80);", "LIMIT(30, 90, 80)")]
        [InlineData("n := MUX(0, 30, 40, 50);", "MUX(0, 30, 40, 50)")]
        [InlineData("f := ABS(-2);", "ABS(-2)")]
        [InlineData("f := SQRT(16);", "SQRT(16)")]
        [InlineData("f := LN(45);", "LN(45)")]
        [InlineData("f := EXP(2);", "EXP(2)")]
        [InlineData("f := SIN(0.5);", "SIN(0.5)")]
        [InlineData("f := TAN(0.5);", "TAN(0.5)")]
        public void ShiftSelectionAndMath(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("n := INT_TO_SINT(4223);", "INT_TO_SINT(4223)")]
        [InlineData("n := TO_INT(4.22);", "TO_INT(4.22)")]
        [InlineData("n := TO___UXINT(n);", "TO___UXINT(n)")]
        [InlineData("b := BYTE_TO_BOOL(2#11010101);", "BYTE_TO_BOOL(2#11010101)")]
        [InlineData("n := BOOL_TO_INT(TRUE);", "BOOL_TO_INT(TRUE)")]
        [InlineData("n := REAL_TO_INT(1.5);", "REAL_TO_INT(1.5)")]
        [InlineData("s := BOOL_TO_STRING(TRUE);", "BOOL_TO_STRING(TRUE)")]
        [InlineData("n := STRING_TO_WORD('34');", "STRING_TO_WORD('34')")]
        [InlineData("t := STRING_TO_TIME('T#127ms');", "STRING_TO_TIME('T#127ms')")]
        [InlineData("n := TIME_TO_DWORD(T#5m);", "TIME_TO_DWORD(T#5m)")]
        [InlineData("n := TOD_TO_SINT(TOD#00:00:00.012);", "TOD_TO_SINT(TOD#00:00:00.012)")]
        [InlineData("n := TRUNC(1.9);", "TRUNC(1.9)")]
        [InlineData("n := TRUNC_INT(1.9);", "TRUNC_INT(1.9)")]
        public void ConversionOperators(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("n := SIZEOF(aArr1);", "SIZEOF(aArr1)")]
        [InlineData("n := XSIZEOF(aData1);", "XSIZEOF(aData1)")]
        [InlineData("n := INDEXOF(fbInst);", "INDEXOF(fbInst)")]
        public void SizeOperators(string source, string fragment) => AssertConstruct(source, fragment);

        [Fact]
        public void DynamicMemory()
        {
            AssertEquivalenceFloor(
                "VAR\npDut : POINTER TO ST_Sample;\npArrayBytes : POINTER TO BYTE;\nEND_VAR\npDut := __NEW(ST_Sample);\npArrayBytes := __NEW(BYTE, 25);\n__DELETE(pDut);",
                "__NEW(ST_Sample)", "__NEW(BYTE, 25)", "__DELETE(pDut)");
        }

        [Fact]
        public void QueryOperators()
        {
            AssertEquivalenceFloor(
                "VAR\nrefInt1 : REFERENCE TO INT;\niBase : I_Base;\niSub : I_Sub;\nEND_VAR\nb1 := __ISVALIDREF(refInt1);\nb2 := __QUERYINTERFACE(iBase, iSub);\nb3 := __QUERYPOINTER(iSub, pFB);",
                "__ISVALIDREF(refInt1)", "__QUERYINTERFACE(iBase, iSub)", "__QUERYPOINTER(iSub, pFB)");
        }

        [Fact]
        public void InfoOperators()
        {
            AssertEquivalenceFloor(
                "s1 := __POUNAME();\ns2 := __POSITION();\ninfo := __VARINFO(nVar);",
                "__POUNAME()", "__POSITION()", "__VARINFO(nVar)");
        }

        [Fact]
        public void TryCatchFinally_Floor()
        {
            AssertEquivalenceFloor(
                "VAR\nexc : __SYSTEM.ExceptionCode;\nEND_VAR\n__TRY\npSample^ := TRUE;\n__CATCH(exc)\nnDivisor := 1;\n__FINALLY\n__ENDTRY",
                "__TRY", "__CATCH(exc)", "__FINALLY", "__ENDTRY");
        }

        [Fact]
        public void Namespaces()
        {
            AssertConstruct("nVar := GVL1.nVar;", "GVL1.nVar");
            AssertConstruct("nVar2 := lib.fbSample(nIn := 22);", "lib.fbSample(nIn := 22)");
            AssertConstruct("eColor := E_Colors.eBlue;", "E_Colors.eBlue");
        }

        [Fact]
        public void BitAccessForms()
        {
            AssertEquivalenceFloor("nVarA.2 := bVarB;", "nVarA.2");
            AssertEquivalenceFloor("nVar.cEnable := TRUE;", "nVar.cEnable");
        }
    }
}
