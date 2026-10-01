using FluentAssertions;
using Xunit;

namespace STFormatterCoreTests.STSyntaxSpec
{
    /// <summary>
    /// 指令与赋值运算符全家族（手册 16.1.3）。每个构造一个用例。
    /// </summary>
    public class StatementsSpecTests : SpecBase
    {
        [Theory]
        [InlineData("x := 1;", "x := 1;")]
        [InlineData("nA := nB := nC + 9;", "nB := nC + 9;")]
        [InlineData("bSet S= bOperand;", "S=")]
        [InlineData("bReset R= bOperand;", "R=")]
        [InlineData("refA REF= stA;", "REF=")]
        public void AssignmentFamily(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("F(x, y => nVar);", "=>")]
        [InlineData("F(x, y => );", "y =>")]
        public void OutputAssignment(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("IF a THEN b := 1; END_IF", "END_IF")]
        [InlineData("IF a THEN b := 1; ELSIF c THEN b := 2; ELSE b := 0; END_IF", "ELSIF")]
        [InlineData("FOR i := 1 TO 5 DO n := n * 2; END_FOR", "TO 5")]
        [InlineData("FOR i := 10 TO 1 BY -1 DO n := n + i; END_FOR", "BY -1")]
        [InlineData("WHILE n <> 0 DO n := n - 1; END_WHILE", "END_WHILE")]
        [InlineData("REPEAT n := n * 2; UNTIL n >= 100 END_REPEAT", "UNTIL n >= 100")]
        [InlineData("REPEAT n := n - 1; UNTIL n <= 0; END_REPEAT", "UNTIL n <= 0;")]
        [InlineData("RETURN;", "RETURN;")]
        [InlineData("EXIT;", "EXIT;")]
        [InlineData("CONTINUE;", "CONTINUE;")]
        [InlineData("IF b THEN RETURN; END_IF", "RETURN;")]
        public void ControlFlow(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("CASE n OF 1: x := 1; END_CASE", "1:")]
        [InlineData("CASE n OF 1,5: x := 1; END_CASE", "1,5:")]
        [InlineData("CASE n OF 1..5: x := 1; END_CASE", "1..5:")]
        [InlineData("CASE n OF 1: x := 1; ELSE x := 0; END_CASE", "ELSE")]
        public void CaseStatement(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("fbTMR(IN := bIn, PT := T#300ms);", "PT := T#300ms")]
        [InlineData("FB_Reset();", "FB_Reset()")]
        [InlineData("pFB^(nIn := 1, nOut => nLoc);", "pFB^")]
        [InlineData("aObjects[2]();", "aObjects[2]()")]
        [InlineData("IF p <> 0 AND_THEN p^ = 99 THEN n := 1; END_IF", "AND_THEN")]
        [InlineData("b := bA OR_ELSE F();", "OR_ELSE")]
        public void CallsAndShortCircuit(string source, string fragment) => AssertConstruct(source, fragment);

        [Fact]
        public void JmpAndLabel_LayoutMayBePlain_ButMustNotBeCorrupted()
        {
            // JMP/labels are a legacy construct the parser treats via recovery;
            // the floor is equivalence, not pretty layout.
            AssertEquivalenceFloor(
                "nVar1 := 0;\n_label1 : nVar1 := nVar1 + 1;\nIF nVar1 < 10 THEN\nJMP _label1;\nEND_IF",
                "JMP _label1;", "_label1");
        }

        [Fact]
        public void AssignmentAsExpression_ExSt()
        {
            AssertEquivalenceFloor("IF b := (i = 1) THEN i := i + 1; END_IF", "b := (i = 1)");
        }

        [Fact]
        public void MultipleSetResetChained()
        {
            AssertEquivalenceFloor(
                "bSet S= bReset R= F(bIn := bVar);",
                "S=", "R=");
        }
    }
}
