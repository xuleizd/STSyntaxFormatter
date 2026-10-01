using System;
using STFormatterCore.Configuration;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Validation;

namespace STFormatterCore.Formatter
{
    /// <summary>
    /// Wraps the raw Lexer → Parser → Formatter pipeline with the
    /// "never corrupt the file" guards (1.8.9 lifted out of the VSIX so both the
    /// extension and the CLI share one testable implementation, the same policy
    /// CSharpier applies on every write-back):
    ///   1. an engine exception → keep the original text and report;
    ///   2. empty or unchanged output → keep the original text;
    ///   3. a failed equivalence validation → keep the original text and report.
    /// A refused file always beats a corrupted one. The delegates are injected so
    /// the guard behaviour itself is unit-testable with mocks.
    /// </summary>
    public sealed class GuardedFormatter
    {
        private readonly Func<string, string> pipeline;
        private readonly Action<string> onFailure;
        private readonly bool validate;

        public GuardedFormatter(
            Func<string, string> pipeline,
            bool validate = true,
            Action<string> onFailure = null)
        {
            this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            this.validate = validate;
            this.onFailure = onFailure;
        }

        /// <summary>
        /// Convenience factory over the real engine pipeline with the given options.
        /// </summary>
        public static GuardedFormatter ForOptions(FormatterOptions options, bool validate = true, Action<string> onFailure = null)
        {
            return new GuardedFormatter(
                source =>
                {
                    var tokens = new STLexer(source).Tokenize();
                    var cst = new STParser(tokens).Parse();
                    return new STFormatter(options).Format(cst, source);
                },
                validate,
                onFailure);
        }

        public string Format(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return source;

            string formatted;
            try
            {
                formatted = pipeline(source);
            }
            catch (Exception ex)
            {
                Report($"格式化引擎异常，已保留原文（{ex.Message}）");
                return source;
            }

            if (string.IsNullOrEmpty(formatted) || formatted == source)
                return source;

            if (validate)
            {
                var validation = FormattingValidator.Validate(source, formatted);
                if (!validation.IsValid)
                {
                    Report($"等价性校验未通过，已保留原文（{validation.FailureMessage}）");
                    return source;
                }
            }

            return formatted;
        }

        private void Report(string message) => onFailure?.Invoke(message);
    }
}
