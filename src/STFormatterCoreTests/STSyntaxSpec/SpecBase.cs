using FluentAssertions;
using STFormatterCore.Configuration;
using STFormatterCore.Formatter;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Validation;

namespace STFormatterCoreTests.STSyntaxSpec
{
    /// <summary>
    /// Shared plumbing for the syntax spec suite (one test per construct from the
    /// TwinCAT 3 PLC manual, see docs/st-syntax-reference.md). Every construct
    /// must satisfy the three invariants:
    ///   1. the construct's tokens survive formatting verbatim;
    ///   2. the equivalence validator passes (no token/tree/comment corruption);
    ///   3. formatting is idempotent.
    /// Assertions use FluentAssertions; random data comes from AutoFixture.
    /// </summary>
    public abstract class SpecBase
    {
        protected static FormatterOptions Options() =>
            new FormatterOptions { LineEnding = LineEnding.LF };

        protected static string Format(string source)
        {
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            return new STFormatter(Options()).Format(cst, source);
        }

        /// <summary>
        /// The standard three-invariant assertion for one construct.
        /// </summary>
        protected static void AssertConstruct(string source, params string[] mustSurvive)
        {
            string formatted = Format(source);

            foreach (var fragment in mustSurvive)
                formatted.Should().Contain(fragment, $"构造片段 {fragment} 必须原样保留");

            var validation = FormattingValidator.Validate(source, formatted, Options());
            validation.IsValid.Should().BeTrue(
                "格式化不得改变语法结构/丢失 token 或注释：{0}", validation.FailureMessage);

            Format(formatted).Should().Be(formatted, "格式化必须是幂等的不动点");
        }

        /// <summary>
        /// The floor assertion for known-unsupported constructs: layout may
        /// degrade, but the validator must still pass and the tokens must survive
        /// (compared with all whitespace stripped, so line breaks and gaps the
        /// formatter introduces in verbatim output do not mask a loss).
        /// </summary>
        protected static void AssertEquivalenceFloor(string source, params string[] mustSurvive)
        {
            string formatted = Format(source);
            string squeezed = System.Text.RegularExpressions.Regex.Replace(formatted, @"\s+", "");

            foreach (var fragment in mustSurvive)
            {
                string squeezedFragment = System.Text.RegularExpressions.Regex.Replace(fragment, @"\s+", "");
                squeezed.Should().Contain(squeezedFragment,
                    $"即便不支持该构造，片段 {fragment} 的 token 也不许丢");
            }

            var validation = FormattingValidator.Validate(source, formatted, Options());
            validation.IsValid.Should().BeTrue(
                "不支持的构造也绝不允许被格式化改坏（等价性底线）：{0}", validation.FailureMessage);
        }
    }
}
