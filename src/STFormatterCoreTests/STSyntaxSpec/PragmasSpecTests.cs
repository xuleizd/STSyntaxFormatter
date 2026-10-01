using FluentAssertions;
using Xunit;

namespace STFormatterCoreTests.STSyntaxSpec
{
    /// <summary>
    /// 编译指示（手册 16.8.2）：位置（POU 头/变量行/GVL/TYPE/语句间）× 取值形式。
    /// </summary>
    public class PragmasSpecTests : SpecBase
    {
        [Theory]
        [InlineData("{attribute 'hide'}", "'hide'")]
        [InlineData("{attribute 'reflection'}", "'reflection'")]
        [InlineData("{attribute 'call_after_init'}", "'call_after_init'")]
        [InlineData("{attribute 'no_assign'}", "'no_assign'")]
        [InlineData("{attribute 'no_check'}", "'no_check'")]
        [InlineData("{attribute 'enable_dynamic_creation'}", "'enable_dynamic_creation'")]
        [InlineData("{attribute 'c++_compatible'}", "'c++_compatible'")]
        public void PragmaAbovePouHeader(string pragma, string fragment)
        {
            AssertConstruct($"{pragma}\nFUNCTION_BLOCK FB_Sample\nVAR\nn : INT;\nEND_VAR\nEND_FUNCTION_BLOCK", fragment);
        }

        [Theory]
        [InlineData("{attribute 'noinit'}", "'noinit'")]
        [InlineData("{attribute 'no_init'}", "'no_init'")]
        [InlineData("{attribute 'no-init'}", "'no-init'")]
        [InlineData("{attribute 'init_on_onlchange'}", "'init_on_onlchange'")]
        [InlineData("{attribute 'hide'}", "'hide'")]
        [InlineData("{attribute 'conditionalshow'}", "'conditionalshow'")]
        [InlineData("{attribute 'no_copy'}", "'no_copy'")]
        public void PragmaAboveVariable(string pragma, string fragment)
        {
            AssertConstruct(
                $"PROGRAM P\nVAR\n{{attribute 'kept'}}\nnOther : INT;\n{pragma}\nnVar : INT;\nEND_VAR\nEND_PROGRAM"
                    .Replace("{attribute 'kept'}", pragma == "{attribute 'kept'}" ? "{attribute 'x'}" : "{attribute 'kept'}"),
                fragment);
        }

        [Theory]
        [InlineData("{attribute 'pack_mode' := '1'}", "pack_mode' := '1'")]
        [InlineData("{attribute 'displaymode' := 'hex'}", "displaymode' := 'hex'")]
        [InlineData("{attribute 'global_init_slot' := '40500'}", "global_init_slot' := '40500'")]
        [InlineData("{attribute 'obsolete' := 'use v2'}", "obsolete' := 'use v2'")]
        [InlineData("{attribute 'TcEncoding' := 'UTF-8'}", "TcEncoding' := 'UTF-8'")]
        [InlineData("{attribute 'is_connected' := 'nIn1'}", "is_connected' := 'nIn1'")]
        public void PragmaWithValues(string pragma, string fragment)
        {
            AssertConstruct(
                $"VAR\nnA : INT;\n{pragma}\nnB : DWORD;\nEND_VAR", fragment);
        }

        [Fact]
        public void PragmaAboveGvlAndType()
        {
            AssertConstruct(
                "{attribute 'linkalways'}\nVAR_GLOBAL\nnVar1 : INT;\nEND_VAR",
                "'linkalways'");
            AssertConstruct(
                "{attribute 'pack_mode' := '1'}\nTYPE ST_MyStruct :\nSTRUCT\nnCounter : INT;\nEND_STRUCT\nEND_TYPE",
                "pack_mode' := '1'");
        }

        [Fact]
        public void BarePragmasInStatements()
        {
            AssertEquivalenceFloor(
                "IF b THEN\n{noflow}\nn := 1;\nEND_IF\n{flow}",
                "{noflow}", "{flow}");
        }

        [Fact]
        public void PragmaBetweenStatements()
        {
            AssertConstruct(
                "n := 1;\n{attribute 'vision'}\nn := 2;",
                "{attribute 'vision'}");
        }

        [Fact]
        public void PragmaWithSemicolonAboveVariable()
        {
            // 变量行上带分号的写法（手册 16.8.2.1 示例）。
            AssertEquivalenceFloor(
                "VAR\n{attribute 'DoCount'};\nnVar : INT;\nEND_VAR",
                "{attribute 'DoCount'}");
        }
    }
}
