using FluentAssertions;
using Xunit;

namespace STFormatterCoreTests.STSyntaxSpec
{
    /// <summary>
    /// 数据类型 / DUT 全家族（手册 16.5）：数组、结构、枚举、别名、UNION、指针、引用、子范围。
    /// </summary>
    public class TypesSpecTests : SpecBase
    {
        [Theory]
        [InlineData("nVarA : INT(-4095..4095);", "INT(-4095..4095)")]
        [InlineData("nVarB : UINT(0..10000);", "UINT(0..10000)")]
        public void InlineSubrange(string declaration, string fragment)
        {
            AssertConstruct($"VAR\n{declaration}\nEND_VAR", fragment);
        }

        [Theory]
        [InlineData("aCounter : ARRAY[0..9] OF INT;", "ARRAY[0..9] OF INT")]
        [InlineData("aCardGame : ARRAY[1..2, 3..4] OF INT;", "ARRAY[1..2, 3..4] OF INT")]
        [InlineData("aBox3 : ARRAY[1..2, 3..4, 5..6] OF INT;", "ARRAY[1..2, 3..4, 5..6] OF INT")]
        [InlineData("aCounter : ARRAY[0..9] OF INT := [0, 10, 20];", ":= [0, 10, 20]")]
        [InlineData("aCardGame : ARRAY[1..2, 3..4] OF INT := [2(10), 2(20)];", "[2(10), 2(20)]")]
        [InlineData("aObjects : ARRAY[1..4] OF FB_Object;", "ARRAY[1..4] OF FB_Object")]
        [InlineData("a2Boxes : ARRAY[1..2] OF ARRAY[1..3] OF INT;", "ARRAY[1..2] OF ARRAY[1..3] OF INT")]
        [InlineData("aData : ARRAY[0..1] OF ST_Data := [(n1 := 1), (n1 := 2)];", "[(n1 := 1), (n1 := 2)]")]
        public void ArrayFamily(string declaration, string fragment)
        {
            AssertConstruct($"VAR\n{declaration}\nEND_VAR", fragment);
        }

        [Theory]
        [InlineData("nLocal1 := aCardGame[1, 3];", "aCardGame[1, 3]")]
        [InlineData("nLocal2 := aBox[1, 3, 5];", "aBox[1, 3, 5]")]
        [InlineData("a2Boxes[1][2] := 1200;", "a2Boxes[1][2]")]
        [InlineData("nLocal3 := aData[1, 1].n1;", "aData[1, 1].n1")]
        [InlineData("pArrayBytes[24] := 125;", "pArrayBytes[24]")]
        public void ArrayAccessForms(string source, string fragment) => AssertConstruct(source, fragment);

        [Fact]
        public void VariableLengthArray()
        {
            AssertConstruct(
                "FUNCTION F_Sum : DINT\nVAR_IN_OUT\naData : ARRAY[*] OF INT;\nEND_VAR\nF_Sum := 0;\nEND_FUNCTION",
                "ARRAY[*] OF INT");
        }

        [Fact]
        public void LowerUpperBound()
        {
            AssertConstruct(
                "FOR i := LOWER_BOUND(aData, 1) TO UPPER_BOUND(aData, 1) DO\nn := aData[i];\nEND_FOR",
                "LOWER_BOUND(aData, 1)", "UPPER_BOUND(aData, 1)");
        }

        [Fact]
        public void PointerFamily()
        {
            AssertConstruct(
                "VAR\npSample : POINTER TO INT;\npStr : POINTER TO STRING(80);\nnAddr : PVOID;\nEND_VAR",
                "POINTER TO INT", "POINTER TO STRING(80)", "PVOID");
        }

        [Theory]
        [InlineData("pSample := ADR(nVar1);", "ADR(nVar1)")]
        [InlineData("nVar2 := pSample^;", "pSample^")]
        [InlineData("nBitoffset := BITADR(bVar1);", "BITADR(bVar1)")]
        public void PointerOperations(string source, string fragment) => AssertConstruct(source, fragment);

        [Fact]
        public void ReferenceFamily()
        {
            AssertConstruct(
                "VAR\nrefInt : REFERENCE TO INT;\nnA : INT;\nEND_VAR\nrefInt REF= nA;\nrefInt := 12;\nnB := refInt * 2;",
                "REFERENCE TO INT", "REF=");
        }

        [Fact]
        public void InterfaceVariable()
        {
            AssertEquivalenceFloor(
                "VAR\nipSample : I_Sample;\nEND_VAR\nIF ipSample <> 0 THEN\nnResult := ipSample.Add(3, 6);\nEND_IF",
                "ipSample.Add(3, 6)");
        }

        [Fact]
        public void StructWithArrayMembers()
        {
            AssertConstruct(
                "TYPE ST_POLYGONLINE :\nSTRUCT\naStart : ARRAY[1..2] OF INT := [-99, -99];\naEnd : ARRAY[1..2] OF INT;\nEND_STRUCT\nEND_TYPE",
                "ARRAY[1..2] OF INT := [-99, -99]");
        }

        [Fact]
        public void StructExtends_Floor()
        {
            // TYPE EXTENDS 走未知 body——底线等价。
            AssertEquivalenceFloor(
                "TYPE ST_PENTAGON EXTENDS ST_POLYGONLINE :\nSTRUCT\naPoint5 : ARRAY[1..2] OF INT;\nEND_STRUCT\nEND_TYPE",
                "EXTENDS ST_POLYGONLINE", "aPoint5");
        }

        [Fact]
        public void StructInitialization()
        {
            AssertEquivalenceFloor(
                "VAR\nstPolygon : ST_POLYGONLINE := (aStart := [1, 1], aEnd := [2, 2]);\nEND_VAR",
                "(aStart := [1, 1], aEnd := [2, 2])");
        }

        [Theory]
        [InlineData("TYPE E_Color : (eRed, eGreen := 10, eBlue); END_TYPE", "eGreen := 10")]
        [InlineData("TYPE E_Color : (eWhite := 16#FFFFFF, eBlack := 16#000000) DWORD; END_TYPE", ") DWORD;")]
        [InlineData("TYPE E_Color : (eRed, eGreen); END_TYPE", "(eRed, eGreen)")]
        public void EnumFamily(string source, string fragment) => AssertConstruct(source, fragment);

        [Fact]
        public void EnumWithPragmas()
        {
            AssertConstruct(
                "{attribute 'qualified_only'}\n{attribute 'strict'}\nTYPE E_Sample : (eInit := 0, eStart, eStop);\nEND_TYPE",
                "{attribute 'qualified_only'}", "{attribute 'strict'}");
        }

        [Fact]
        public void EnumQualifiedAccess() => AssertConstruct("eFlower := E_ColorBasic.eBlue;", "E_ColorBasic.eBlue");

        [Fact]
        public void EnumDefaultComponentInit_Floor()
        {
            // `) DWORD := eBlack;` 的 := 部分未解析——底线等价。
            AssertEquivalenceFloor(
                "TYPE E_Color : (eWhite := 1, eBlack := 2) DWORD := eBlack;\nEND_TYPE",
                "eBlack");
        }

        [Fact]
        public void AliasWithStringLength()
        {
            AssertConstruct("TYPE T_Message : STRING[50];\nEND_TYPE", "STRING[50]");
        }

        [Fact]
        public void UnionFamily()
        {
            AssertConstruct(
                "TYPE U_Name :\nUNION\nfA : LREAL;\nnB : LINT;\nEND_UNION\nEND_TYPE",
                "UNION", "END_UNION");
        }

        [Fact]
        public void SystemTypes()
        {
            AssertEquivalenceFloor(
                "VAR\nexc : __SYSTEM.ExceptionCode;\nn : __UXINT;\nw : __XWORD;\nEND_VAR\nexc := __SYSTEM.ExceptionCode.RTSEXCPT_ARRAYBOUNDS;",
                "__SYSTEM.ExceptionCode.RTSEXCPT_ARRAYBOUNDS");
        }

        [Fact]
        public void BitTypeMembers()
        {
            AssertConstruct(
                "TYPE ST_S :\nSTRUCT\nbReady : BIT;\nEND_STRUCT\nEND_TYPE",
                "bReady : BIT;");
            AssertEquivalenceFloor("stS.bReady := TRUE;", "stS.bReady := TRUE;");
        }
    }
}
