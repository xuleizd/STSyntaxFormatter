using AutoFixture;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace STFormatterCoreTests.STSyntaxSpec
{
    /// <summary>
    /// 字面量全家族（手册 16.4）。每个构造一个用例；数值类构造再叠一层
    /// AutoFixture 随机值——同一模板换任意数据，三不变式都必须成立。
    /// </summary>
    public class LiteralsSpecTests : SpecBase
    {
        [Theory]
        [InlineData("b := TRUE;", "TRUE")]
        [InlineData("b := FALSE;", "FALSE")]
        public void BoolConstants(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("n := 42;", "42")]
        [InlineData("n := 1_000_000;", "1_000_000")]
        [InlineData("n := 2#1001_0011;", "2#1001_0011")]
        [InlineData("n := 8#67;", "8#67")]
        [InlineData("n := 16#A;", "16#A")]
        [InlineData("n := 16#FFFF_FFFF;", "16#FFFF_FFFF")]
        [InlineData("n := DINT#16#A1;", "DINT#16#A1")]
        public void NumericConstants(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("r := 7.4;", "7.4")]
        [InlineData("r := -1E-44;", "1E-44")]
        [InlineData("r := 1.64e+009;", "1.64e+009")]
        [InlineData("r := REAL#3.14;", "REAL#3.14")]
        [InlineData("n := DINT#34;", "DINT#34")]
        [InlineData("b := BOOL#TRUE;", "BOOL#TRUE")]
        public void RealAndTypedConstants(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("s := 'Hello World!';", "'Hello World!'")]
        [InlineData("ws := \"black\";", "\"black\"")]
        [InlineData("s := 'a$Lb$Tc$$d$'e$41';", "$L")]
        [InlineData("s := 'It''s';", "It''s")]
        public void StringConstants(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("t := T#14ms;", "T#14ms")]
        [InlineData("t := TIME#12h34m15s;", "TIME#12h34m15s")]
        [InlineData("t := T#49D17H2M47S295MS;", "T#49D17H2M47S295MS")]
        [InlineData("lt := LTIME#1000d15h23m12s34ms2us44ns;", "LTIME#1000d15h23m12s34ms2us44ns")]
        public void TimeConstants(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("d := DATE#2018-8-8;", "DATE#2018-8-8")]
        [InlineData("d := D#2018-8-31;", "D#2018-8-31")]
        [InlineData("d := date#1996-05-06;", "date#1996-05-06")]
        [InlineData("d := LDATE#2018-8-8;", "LDATE#2018-8-8")]
        [InlineData("d := LD#1996-05-06;", "LD#1996-05-06")]
        [InlineData("dt := DATE_AND_TIME#1996-05-06-15:36:30;", "DATE_AND_TIME#1996-05-06-15:36:30")]
        [InlineData("dt := DT#1972-03-29-00:00:00;", "DT#1972-03-29-00:00:00")]
        [InlineData("dt := DT#2018-08-08-13:33:20.5;", "DT#2018-08-08-13:33:20.5")]
        [InlineData("t := TIME_OF_DAY#15:36:30.123;", "TIME_OF_DAY#15:36:30.123")]
        [InlineData("t := TOD#12:34:56.789;", "TOD#12:34:56.789")]
        [InlineData("dt := LDT#2020-2-7-12:55:1.234567891;", "LDT#2020-2-7-12:55:1.234567891")]
        [InlineData("t := LTOD#12:3:4.567890123;", "LTOD#12:3:4.567890123")]
        public void DateAndTimeConstants(string source, string fragment) => AssertConstruct(source, fragment);

        [Theory]
        [InlineData("b AT %IX0.0 : BOOL;", "%IX0.0")]
        [InlineData("b AT %QX7.5 : BOOL;", "%QX7.5")]
        [InlineData("b AT %Q7.5 : BOOL;", "%Q7.5")]
        [InlineData("n AT %IW0 : INT;", "%IW0")]
        [InlineData("b AT %QB0 : BOOL;", "%QB0")]
        [InlineData("n AT %MD48 : DINT;", "%MD48")]
        [InlineData("b AT %I* : BOOL;", "%I*")]
        [InlineData("b AT%I* : BOOL;", "%I*")]
        [InlineData("w AT %IW2.5.7.1 : WORD;", "%IW2.5.7.1")]
        public void DirectAddresses(string declaration, string fragment)
        {
            AssertConstruct($"PROGRAM P\nVAR\n{declaration}\nEND_VAR\nEND_PROGRAM", fragment);
        }

        [Fact]
        public void FunctionCallAsOperand() => AssertConstruct("n := F_Add(7, 5) + 3;", "F_Add(7, 5) + 3");

        #region AutoFixture randomized

        private static readonly Fixture Fixture = new Fixture();

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(42)]
        public void RandomDecimalValues_SurviveEverywhere(int seed)
        {
            int value = Fixture.Create<int>();
            AssertConstruct($"n := {value};", value.ToString());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void RandomHexValues_SurviveVerbatim(int _)
        {
            int value = Fixture.Create<int>() & 0xFFFFFF;
            string hex = $"16#{value:X}";
            AssertConstruct($"n := {hex};", hex);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void RandomRealValues_SurviveVerbatim(int _)
        {
            double value = Fixture.Create<double>();
            string real = value.ToString("0.0######", System.Globalization.CultureInfo.InvariantCulture);
            AssertConstruct($"r := {real};", real);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void RandomMilliseconds_SurviveVerbatim(int _)
        {
            int ms = Fixture.Create<int>() & 0x7FFF;
            Assert.True(ms >= 0);
            AssertConstruct($"t := T#{ms}ms;", $"T#{ms}ms");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void RandomIdentifiers_SurviveInEveryPosition(int _)
        {
            string name = "n_" + new string(
                Fixture.CreateMany<char>(6).Select(c => char.ToLowerInvariant(char.IsLetter(c) ? c : 'x')).ToArray());
            AssertConstruct($"{name} := {name} + 1;", $"{name} := {name} + 1;");
        }

        #endregion
    }
}
