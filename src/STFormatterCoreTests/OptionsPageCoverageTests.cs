using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using STFormatterCore.Configuration;
using STFormatterCore.Formatter;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using Xunit;

namespace STFormatterCoreTests
{
    /// <summary>
    /// Systematic coverage of every option shown in the VSIX options page
    /// (工具 → 选项 → ST 格式化, see STFormatterVSIX/OptionsPage.cs).
    /// One group per option, one test per value. The page exposes 14 settings;
    /// 13 of them map to FormatterOptions and are covered behaviorally here,
    /// plus a mapping guard test. "保存时自动格式化" (FormatOnSave) is VSIX
    /// runtime behavior (document-save hook) and cannot be exercised in Core.
    /// </summary>
    public class OptionsPageCoverageTests
    {
        private string Format(string source, FormatterOptions options = null)
        {
            options = options ?? new FormatterOptions();
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            return new STFormatter(options).Format(cst, source);
        }

        private static FormatterOptions Lf(FormatterOptions o)
        {
            o.LineEnding = LineEnding.LF;
            return o;
        }

        #region 缩进 → 使用空格 (UseSpacesInsteadOfTab)

        [Fact]
        public void UseSpacesInsteadOfTab_True_IndentsWithSpaces()
        {
            var source = "PROGRAM P\nIF a THEN\nx := 1;\nEND_IF\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { UseSpacesInsteadOfTab = true, IndentSize = 4 }));
            Assert.Contains("\n        x := 1;", result); // IF body = 2 levels of 4 spaces
            Assert.DoesNotContain("\t", result);
        }

        [Fact]
        public void UseSpacesInsteadOfTab_False_IndentsWithTabs()
        {
            var source = "PROGRAM P\nIF a THEN\nx := 1;\nEND_IF\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { UseSpacesInsteadOfTab = false }));
            Assert.Contains("\n\t\tx := 1;", result); // one tab per level
        }

        #endregion

        #region 缩进 → 缩进空格数 (IndentSize)

        [Fact]
        public void IndentSize_2_TwoSpacesPerLevel()
        {
            var source = "PROGRAM P\nIF a THEN\nx := 1;\nEND_IF\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { IndentSize = 2 }));
            Assert.Contains("\n    x := 1;", result);   // IF at 2, body at 4
            Assert.Contains("\n  IF a THEN", result);
        }

        [Fact]
        public void IndentSize_4_Default_FourSpacesPerLevel()
        {
            var source = "PROGRAM P\nIF a THEN\nx := 1;\nEND_IF\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { IndentSize = 4 }));
            Assert.Contains("\n    IF a THEN", result);
            Assert.Contains("\n        x := 1;", result);
        }

        [Fact]
        public void IndentSize_8_EightSpacesPerLevel()
        {
            var source = "PROGRAM P\nIF a THEN\nx := 1;\nEND_IF\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { IndentSize = 8 }));
            Assert.Contains("\n        IF a THEN", result);
            Assert.Contains("\n                x := 1;", result);
        }

        #endregion

        #region 格式化 → 运算符两侧加空格 (OperatorSpacing)

        [Fact]
        public void OperatorSpacing_True_StatementHasSpacesAroundOperators()
        {
            var source = "PROGRAM P\nx:=1;\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { OperatorSpacing = true }));
            Assert.Contains("x := 1;", result);
        }

        [Fact]
        public void OperatorSpacing_True_VarInitHasSpacesAroundAssign()
        {
            var source = "PROGRAM P\nVAR\nx : INT := 5;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { OperatorSpacing = true }));
            Assert.Contains(":= 5;", result);
        }

        [Fact]
        public void OperatorSpacing_False_VarInitHasNoSpacesAroundAssign()
        {
            var source = "PROGRAM P\nVAR\nx : INT := 5;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { OperatorSpacing = false }));
            Assert.Contains(":=5;", result);
        }

        [Fact]
        public void OperatorSpacing_False_StatementBehavior()
        {
            var source = "PROGRAM P\nx := 1;\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { OperatorSpacing = false }));
            // The option is documented as "在运算符两侧添加空格"; with it off the
            // statement should not gain operator spaces: x:=1;
            Assert.Contains("x:=1;", result);
        }

        [Fact]
        public void OperatorSpacing_BothValues_ParenthesizedVarInitSurvives()
        {
            // The ':=' inside a parenthesized initialization are argument
            // assignments, not the declaration's init separator — the line must
            // keep them under both option values.
            var source = "PROGRAM P\nVAR\nfb : FB_Demo(sNetID:='', tTimeout:=T#5S);\nEND_VAR\nEND_PROGRAM";
            foreach (var spacing in new[] { true, false })
            {
                var result = Format(source, Lf(new FormatterOptions { OperatorSpacing = spacing }));
                Assert.Contains("FB_Demo(", result);
                Assert.Contains("sNetID", result);
                Assert.Contains(":=", result);
                Assert.Contains("''", result);
                Assert.Contains("T#5S", result);
                Assert.Equal(2, CountIn(result, ":="));
            }
        }

        private static int CountIn(string text, string needle)
        {
            int count = 0, index = 0;
            while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }

        #endregion

        #region 格式化 → 逗号后加空格 (CommaSpacing)

        [Fact]
        public void CommaSpacing_True_SpaceAfterComma()
        {
            var source = "PROGRAM P\nFunc(a,b);\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { CommaSpacing = true }));
            Assert.Contains("Func(a, b);", result);
        }

        [Fact]
        public void CommaSpacing_False_NoSpaceAfterComma()
        {
            var source = "PROGRAM P\nFunc(a, b);\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { CommaSpacing = false }));
            Assert.Contains("Func(a,b);", result);
        }

        #endregion

        #region 格式化 → 括号内侧空格 (ParenInnerSpacing)

        [Fact]
        public void ParenInnerSpacing_True_SpacesInsideParens()
        {
            var source = "PROGRAM P\ny := (a);\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { ParenInnerSpacing = true }));
            Assert.Contains("( a )", result);
        }

        [Fact]
        public void ParenInnerSpacing_False_NoSpacesInsideParens()
        {
            var source = "PROGRAM P\ny := ( a );\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { ParenInnerSpacing = false }));
            Assert.Contains("(a)", result);
        }

        #endregion

        #region 格式化 → 对齐变量声明 (AlignDeclarations)

        [Fact]
        public void AlignDeclarations_True_ColonsAligned()
        {
            var source = "PROGRAM P\nVAR\nxx : INT;\ny : BOOL;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { AlignDeclarations = true }));
            var lines = result.Split('\n');
            var xxLine = lines.Single(l => l.Contains("xx"));
            var yLine = lines.Single(l => l.TrimStart().StartsWith("y "));
            Assert.Equal(xxLine.IndexOf(':'), yLine.IndexOf(':'));
            Assert.Contains("xx : INT;", result);
            Assert.Contains("y  : BOOL;", result); // padded to the longest name
        }

        [Fact]
        public void AlignDeclarations_False_SingleSpaceBeforeColon()
        {
            var source = "PROGRAM P\nVAR\nxx : INT;\ny : BOOL;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { AlignDeclarations = false }));
            Assert.Contains("y : BOOL;", result);
            Assert.DoesNotContain("y  :", result);
        }

        #endregion

        #region 格式化 → 保留空行 (KeepEmptyLines)

        [Fact]
        public void KeepEmptyLines_True_SourceBlankLineSurvives()
        {
            var source = "PROGRAM P\nx := 1;\n\ny := 2;\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { KeepEmptyLines = true }));
            Assert.Contains("x := 1;\n\n    y := 2;", result);
        }

        [Fact]
        public void KeepEmptyLines_False_SourceBlankLinesRemoved()
        {
            var source = "PROGRAM P\nx := 1;\n\ny := 2;\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { KeepEmptyLines = false }));
            Assert.DoesNotContain("\n\n", result);
            Assert.Contains("x := 1;", result);
            Assert.Contains("y := 2;", result);
        }

        #endregion

        #region 格式化 → VAR 块后空行数 (BlankLinesAfterVar)

        [Fact]
        public void BlankLinesAfterVar_0_NoBlankAfterEndVar()
        {
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\ny := 1;\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { BlankLinesAfterVar = 0, KeepEmptyLines = false }));
            Assert.Contains("END_VAR\n    y := 1;", result);
        }

        [Fact]
        public void BlankLinesAfterVar_1_OneBlankAfterEndVar()
        {
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\ny := 1;\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { BlankLinesAfterVar = 1, KeepEmptyLines = false }));
            Assert.Contains("END_VAR\n\n    y := 1;", result);
        }

        #endregion

        #region 格式化 → END 前空行数 (BlankLinesBeforeEnd)

        [Fact]
        public void BlankLinesBeforeEnd_0_NoBlankBeforeEndProgram()
        {
            var source = "PROGRAM P\nx := 1;\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { BlankLinesBeforeEnd = 0, KeepEmptyLines = false }));
            Assert.Contains("x := 1;\nEND_PROGRAM", result);
        }

        [Fact]
        public void BlankLinesBeforeEnd_1_OneBlankBeforeEndProgram()
        {
            var source = "PROGRAM P\nx := 1;\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { BlankLinesBeforeEnd = 1, KeepEmptyLines = false }));
            Assert.Contains("x := 1;\n\nEND_PROGRAM", result);
        }

        #endregion

        #region 格式化 → 语句块前后空行 (BlankLinesAroundStatementBlocks)

        [Fact]
        public void BlankLinesAroundStatementBlocks_True_BlankAroundIfBlock()
        {
            var source = "PROGRAM P\nx := 1;\nIF a THEN\ny := 2;\nEND_IF\nz := 3;\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { BlankLinesAroundStatementBlocks = true, KeepEmptyLines = false }));
            Assert.Contains("x := 1;\n\n    IF a THEN", result);
            Assert.Contains("END_IF\n\n    z := 3;", result);
        }

        [Fact]
        public void BlankLinesAroundStatementBlocks_False_NoInsertedBlanks()
        {
            var source = "PROGRAM P\nx := 1;\nIF a THEN\ny := 2;\nEND_IF\nz := 3;\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { BlankLinesAroundStatementBlocks = false, KeepEmptyLines = false }));
            Assert.DoesNotContain("\n\n", result);
        }

        #endregion

        #region 格式化 → 最大行长度 (MaxLineLength)

        [Fact]
        public void MaxLineLength_Small_WrapsAfterComma()
        {
            // The argument line reaches the wrap threshold (MaxLineLength minus
            // IndentSize, measured before the comma) only for small limits: with
            // MaxLineLength=28 the call breaks after the comma and the next
            // argument continues on its own indented line.
            var source = "PROGRAM P\nIF x THEN\nFunc(aaaaaaaaaa, bbbbbbbbbb);\nEND_IF\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { MaxLineLength = 28, IndentSize = 4 }));
            Assert.Contains("Func(aaaaaaaaaa,\n             bbbbbbbbbb);", result);
        }

        [Fact]
        public void MaxLineLength_Medium_NoWrapBelowThreshold()
        {
            // Same call with the default-scale limit stays on one line.
            var source = "PROGRAM P\nIF x THEN\nFunc(aaaaaaaaaa, bbbbbbbbbb);\nEND_IF\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { MaxLineLength = 40, IndentSize = 4 }));
            Assert.Contains("Func(aaaaaaaaaa, bbbbbbbbbb);", result);
        }

        [Fact]
        public void MaxLineLength_Large_NoWrap()
        {
            var source = "PROGRAM P\nFunc(aaaaaaaaaa, bbbbbbbbbb);\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { MaxLineLength = 200 }));
            Assert.Contains("Func(aaaaaaaaaa, bbbbbbbbbb);", result);
        }

        [Fact]
        public void MaxLineLength_WrapIsIdempotent()
        {
            var source = "PROGRAM P\nIF x THEN\nFunc(aaaaaaaaaa, bbbbbbbbbb);\nEND_IF\nEND_PROGRAM";
            var options = Lf(new FormatterOptions { MaxLineLength = 28, IndentSize = 4 });
            var once = Format(source, options);
            var twice = Format(once, options);
            Assert.Equal(once, twice);
        }

        #endregion

        #region 格式化 → 换行符 (LineEnding)

        [Fact]
        public void LineEnding_CRLF_OutputUsesCrlf()
        {
            var source = "PROGRAM P\nx := 1;\nEND_PROGRAM"; // LF source
            var result = Format(source, new FormatterOptions { LineEnding = LineEnding.CRLF });
            Assert.Contains("\r\n", result);
            Assert.DoesNotContain("\n", result.Replace("\r\n", ""));
        }

        [Fact]
        public void LineEnding_LF_OutputUsesLf()
        {
            var source = "PROGRAM P\r\nx := 1;\r\nEND_PROGRAM"; // CRLF source
            var result = Format(source, new FormatterOptions { LineEnding = LineEnding.LF });
            Assert.DoesNotContain("\r\n", result);
        }

        [Fact]
        public void LineEnding_Auto_FollowsLfSource()
        {
            var source = "PROGRAM P\nx := 1;\nEND_PROGRAM";
            var result = Format(source, new FormatterOptions { LineEnding = LineEnding.Auto });
            Assert.DoesNotContain("\r\n", result);
            Assert.Contains("\n", result);
        }

        [Fact]
        public void LineEnding_Auto_FollowsCrlfSource()
        {
            var source = "PROGRAM P\r\nx := 1;\r\nEND_PROGRAM";
            var result = Format(source, new FormatterOptions { LineEnding = LineEnding.Auto });
            Assert.Contains("\r\n", result);
        }

        #endregion

        #region 格式化 → 类型大小写 (TypeCase)

        [Fact]
        public void TypeCase_Upper_TypeNamesUppercased()
        {
            var source = "PROGRAM P\nVAR\nx : int;\narr : array[0..1] of bool;\np : pointer to int;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { TypeCase = TypeCase.Upper }));
            Assert.Contains("INT", result);
            Assert.Contains("ARRAY[0 .. 1] OF BOOL", result);
            Assert.Contains("POINTER TO INT", result);
        }

        [Fact]
        public void TypeCase_Lower_TypeNamesLowercased()
        {
            var source = "PROGRAM P\nVAR\nx : INT;\narr : ARRAY[0..1] OF BOOL;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { TypeCase = TypeCase.Lower }));
            Assert.Contains("int", result);
            Assert.Contains("array[0 .. 1] of bool", result);
            Assert.DoesNotContain("ARRAY", result);
        }

        [Fact]
        public void TypeCase_Preserve_TypeNamesUnchanged()
        {
            var source = "PROGRAM P\nVAR\nx : Int;\narr : Array[0..1] Of Bool;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, Lf(new FormatterOptions { TypeCase = TypeCase.Preserve }));
            Assert.Contains("Int", result);
            Assert.Contains("Of Bool", result);
        }

        #endregion

        #region 选项页 ↔ FormatterOptions 映射守卫

        /// <summary>
        /// The option names shown in 工具 → 选项 (OptionsPage.cs) must exist on
        /// FormatterOptions with the same type, so a renamed/removed option
        /// cannot silently disconnect the UI from the engine. FormatOnSave is
        /// VSIX-only (save hook) and intentionally not part of FormatterOptions.
        /// </summary>
        [Fact]
        public void OptionsPage_AllMappedOptions_ExistOnFormatterOptions()
        {
            var pageOptions = new Dictionary<string, Type>
            {
                { "UseSpacesInsteadOfTab", typeof(bool) },
                { "IndentSize", typeof(int) },
                { "OperatorSpacing", typeof(bool) },
                { "CommaSpacing", typeof(bool) },
                { "ParenInnerSpacing", typeof(bool) },
                { "AlignDeclarations", typeof(bool) },
                { "MaxLineLength", typeof(int) },
                { "LineEnding", typeof(LineEnding) },
                { "KeepEmptyLines", typeof(bool) },
                { "BlankLinesAfterVar", typeof(int) },
                { "BlankLinesBeforeEnd", typeof(int) },
                { "BlankLinesAroundStatementBlocks", typeof(bool) },
                { "TypeCase", typeof(TypeCase) },
            };

            foreach (var kv in pageOptions)
            {
                var prop = typeof(FormatterOptions).GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                Assert.True(prop != null, $"FormatterOptions is missing option '{kv.Key}' shown in the options page");
                Assert.True(prop.PropertyType == kv.Value, $"Option '{kv.Key}' changed type: expected {kv.Value.Name}, got {prop.PropertyType.Name}");
                Assert.True(prop.CanRead && prop.CanWrite, $"Option '{kv.Key}' must be get/set");
            }
        }

        /// <summary>
        /// Every settable option on FormatterOptions must be exposed in the
        /// options page list above — a new engine option without UI coverage
        /// fails this test until it is added to OptionsPage (and to this list).
        /// </summary>
        [Fact]
        public void FormatterOptions_AllSettableOptions_AreCoveredHere()
        {
            var covered = new HashSet<string>
            {
                "UseSpacesInsteadOfTab", "IndentSize", "MaxLineLength", "LineEnding",
                "OperatorSpacing", "CommaSpacing", "ParenInnerSpacing",
                "AlignDeclarations", "BlankLinesBeforeEnd", "BlankLinesAfterVar",
                "KeepEmptyLines", "BlankLinesAroundStatementBlocks", "TypeCase",
            };

            var settable = typeof(FormatterOptions)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .Select(p => p.Name)
                .ToList();

            foreach (var name in settable)
                Assert.Contains(name, covered);
        }

        /// <summary>
        /// The defaults the options page starts with must equal the engine
        /// defaults, otherwise the UI shows values the engine does not use.
        /// </summary>
        [Fact]
        public void OptionsPage_Defaults_MatchFormatterOptionsDefaults()
        {
            var o = new FormatterOptions();
            Assert.True(o.UseSpacesInsteadOfTab);
            Assert.Equal(4, o.IndentSize);
            Assert.True(o.OperatorSpacing);
            Assert.True(o.CommaSpacing);
            Assert.False(o.ParenInnerSpacing);
            Assert.True(o.AlignDeclarations);
            Assert.Equal(120, o.MaxLineLength);
            Assert.Equal(LineEnding.Auto, o.LineEnding);
            Assert.True(o.KeepEmptyLines);
            Assert.Equal(0, o.BlankLinesAfterVar);
            Assert.Equal(0, o.BlankLinesBeforeEnd);
            Assert.True(o.BlankLinesAroundStatementBlocks);
            Assert.Equal(TypeCase.Preserve, o.TypeCase);
        }

        #endregion
    }
}
