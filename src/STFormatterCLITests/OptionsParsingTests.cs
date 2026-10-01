using System.Linq;
using CommandLine;
using STFormatterCLI;
using Xunit;

namespace STFormatterCLITests
{
    /// <summary>
    /// Parses the CLI option surface the way Program.Main does. Since 1.8.9 the
    /// boolean layout options are nullable switches that accept an explicit
    /// false (--keep-empty-lines false), and the two placebo options
    /// (--lowercasekeywords / --safe) are gone for good — --help must not
    /// advertise anything the tool cannot do.
    /// </summary>
    public class OptionsParsingTests
    {
        private static Options Parse(params string[] args)
        {
            var parsed = Parser.Default.ParseArguments<Options>(args)
                .MapResult(o => o, errors => null);
            Assert.NotNull(parsed);
            return parsed;
        }

        [Fact]
        public void KeepEmptyLines_DefaultsToNull_EngineDefaultApplies()
        {
            var options = Parse("-f", "x.TcPOU");
            Assert.Null(options.KeepEmptyLines);
        }

        [Theory]
        [InlineData("false", false)]
        [InlineData("true", true)]
        public void KeepEmptyLines_AcceptsExplicitBool(string value, bool expected)
        {
            // Before 1.8.9 this was a plain switch: there was no way to turn it
            // OFF from the command line even though the engine supports it.
            var options = Parse("-f", "x.TcPOU", "--keep-empty-lines", value);
            Assert.Equal(expected, options.KeepEmptyLines);
        }

        [Theory]
        [InlineData("false", false)]
        [InlineData("true", true)]
        public void AlignDeclarations_AcceptsExplicitBool(string value, bool expected)
        {
            var options = Parse("-f", "x.TcPOU", "--align-declarations", value);
            Assert.Equal(expected, options.AlignDeclarations);
        }

        [Fact]
        public void SkipValidation_ParsesAsASwitch()
        {
            var options = Parse("-f", "x.TcPOU", "--skip-validation");
            Assert.True(options.SkipValidation);
        }

        [Fact]
        public void PlaceboOptions_AreGone()
        {
            // --lowercasekeywords and --safe used to be accepted and silently
            // ignored (keywords are always uppercase; "safe mode" never existed).
            Assert.Null(typeof(Options).GetProperty("LowercaseKeywords"));
            Assert.Null(typeof(Options).GetProperty("Safe"));

            var result = Parser.Default.ParseArguments<Options>(
                new[] { "-f", "x.TcPOU", "--lowercasekeywords" });
            Assert.True(result.Tag == ParserResultType.NotParsed);
        }

        [Fact]
        public void HelpText_DoesNotAdvertiseUnsupportedThings()
        {
            var helpText = typeof(Options)
                .GetProperties()
                .SelectMany(p => p.GetCustomAttributes(typeof(OptionAttribute), false)
                    .OfType<OptionAttribute>())
                .Select(a => a.HelpText)
                .ToList();
            Assert.All(helpText, text => Assert.DoesNotContain("NOT supported", text));
        }
    }
}
