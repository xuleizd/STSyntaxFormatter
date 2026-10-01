using System;
using FluentAssertions;
using Moq;
using STFormatterCore.Configuration;
using STFormatterCore.Formatter;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Validation;
using Xunit;

namespace STFormatterCoreTests.STSyntaxSpec
{
    /// <summary>
    /// GuardedFormatter is the shared write-back guard (1.8.9, lifted from the
    /// VSIX into Core). Moq drives the pipeline/failure delegates so the guard
    /// matrix itself is pinned: exception / empty / unchanged / invalid output
    /// all keep the original text, valid output passes through.
    /// </summary>
    public class GuardedFormatterTests
    {
        [Fact]
        public void PipelineThrows_KeepsOriginal_AndReportsFailure()
        {
            var pipeline = new Mock<Func<string, string>>();
            pipeline.Setup(p => p("x := 1;")).Throws(new InvalidOperationException("boom"));
            var onFailure = new Mock<Action<string>>();

            var guarded = new GuardedFormatter(pipeline.Object, validate: true, onFailure: onFailure.Object);
            var result = guarded.Format("x := 1;");

            result.Should().Be("x := 1;", "引擎异常必须保留原文");
            onFailure.Verify(f => f(It.Is<string>(m => m.Contains("格式化引擎异常") && m.Contains("boom"))), Times.Once);
        }

        [Fact]
        public void PipelineReturnsEmpty_KeepsOriginal_WithoutFailureReport()
        {
            var pipeline = new Mock<Func<string, string>>();
            pipeline.Setup(p => p(It.IsAny<string>())).Returns(string.Empty);
            var onFailure = new Mock<System.Action<string>>();

            var guarded = new GuardedFormatter(pipeline.Object, validate: true, onFailure: onFailure.Object);
            var result = guarded.Format("x := 1;");

            result.Should().Be("x := 1;", "空输出必须保留原文");
            onFailure.Verify(f => f(It.IsAny<string>()), Times.Never, "空输出不算失败，不该报告");
        }

        [Fact]
        public void PipelineReturnsSameText_KeepsOriginal_WithoutValidation()
        {
            var pipeline = new Mock<Func<string, string>>();
            pipeline.Setup(p => p("x := 1;")).Returns("x := 1;");

            var guarded = new GuardedFormatter(pipeline.Object, validate: true);
            var result = guarded.Format("x := 1;");

            result.Should().Be("x := 1;");
            pipeline.Verify(p => p("x := 1;"), Times.Once);
        }

        [Fact]
        public void ValidationFails_KeepsOriginal_AndReportsFailure()
        {
            // A pipeline that corrupts: drops a token. The guard must refuse it.
            var pipeline = new Mock<Func<string, string>>();
            pipeline.Setup(p => p(It.IsAny<string>())).Returns("y := 2;");
            var onFailure = new Mock<System.Action<string>>();

            var guarded = new GuardedFormatter(pipeline.Object, validate: true, onFailure: onFailure.Object);
            var result = guarded.Format("x := 1; // note");

            result.Should().Be("x := 1; // note", "校验失败必须保留原文");
            onFailure.Verify(f => f(It.Is<string>(m => m.Contains("等价性校验未通过"))), Times.Once);
        }

        [Fact]
        public void ValidationDisabled_CorruptedOutputPassesThrough()
        {
            var pipeline = new Mock<Func<string, string>>();
            pipeline.Setup(p => p(It.IsAny<string>())).Returns("y := 2;");

            var guarded = new GuardedFormatter(pipeline.Object, validate: false);
            var result = guarded.Format("x := 1;");

            result.Should().Be("y := 2;", "显式关闭校验时输出原样通过（逃生门）");
        }

        [Fact]
        public void ValidOutput_PassesThrough()
        {
            string valid = FormatReal("x:=1;");
            var pipeline = new Mock<Func<string, string>>();
            pipeline.Setup(p => p("x:=1;")).Returns(valid);

            var guarded = new GuardedFormatter(pipeline.Object, validate: true);
            guarded.Format("x:=1;").Should().Be(valid);
        }

        [Fact]
        public void WhitespaceOnlySource_ReturnsAsIs_WithoutPipelineCall()
        {
            var pipeline = new Mock<Func<string, string>>();
            var guarded = new GuardedFormatter(pipeline.Object);

            guarded.Format("   ").Should().Be("   ");
            pipeline.Verify(p => p(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ForOptions_RealEngine_EndToEnd()
        {
            var onFailure = new Mock<System.Action<string>>();
            var guarded = GuardedFormatter.ForOptions(
                new FormatterOptions { LineEnding = LineEnding.LF }, validate: true, onFailure: onFailure.Object);

            var result = guarded.Format("program P\nvar x:int; end_var\nend_program");

            result.Should().Contain("PROGRAM P").And.Contain("x : int;");
            onFailure.Verify(f => f(It.IsAny<string>()), Times.Never);
        }

        private static string FormatReal(string source) =>
            new STFormatter(new FormatterOptions { LineEnding = LineEnding.LF })
                .Format(new STParser(new STLexer(source).Tokenize()).Parse(), source);
    }
}
