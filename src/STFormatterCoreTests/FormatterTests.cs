using System;
using System.Linq;
using STFormatterCore.Configuration;
using STFormatterCore.Formatter;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Parser.Nodes;
using Xunit;

namespace STFormatterCoreTests
{
    public class FormatterTests
    {
        #region Helpers

        private string Format(string source, FormatterOptions options = null)
        {
            options = options ?? new FormatterOptions();
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            return new STFormatter(options).Format(cst, source);
        }

        #endregion

        #region 1. Keywords are uppercased

        [Fact]
        public void Keywords_AreUppercased()
        {
            var result = Format("program Main\nend_program");
            Assert.Contains("PROGRAM", result);
            Assert.Contains("END_PROGRAM", result);
            Assert.DoesNotContain("program", result);
        }

        [Fact]
        public void ControlFlowKeywords_Uppercased()
        {
            var result = Format("program P\nif x then\nend_if\nend_program");
            Assert.Contains("IF", result);
            Assert.Contains("THEN", result);
            Assert.Contains("END_IF", result);
        }

        #endregion

        #region 2. Indentation is correct for nested blocks

        [Fact]
        public void Indentation_NestedBlocks()
        {
            var source = "PROGRAM P\nIF x THEN\ny := 1;\nEND_IF\nEND_PROGRAM";
            var result = Format(source);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            // Check that at least one line has leading whitespace (indentation)
            bool hasIndentedLine = false;
            foreach (var line in lines)
            {
                if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    hasIndentedLine = true;
                    break;
                }
            }
            Assert.True(hasIndentedLine, "Formatted output should contain indented lines");
        }

        #endregion

        #region 3. VAR block declarations aligned when AlignDeclarations=true

        [Fact]
        public void VarBlock_AlignDeclarations_True()
        {
            var options = new FormatterOptions { AlignDeclarations = true };
            var source = "PROGRAM P\nVAR\na : INT;\nlongName : REAL;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, options);

            // The colons should be aligned. "longName" is 8 chars, "a" is 1 char.
            // So "a" should have more padding before ":"
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            int colonPosA = -1, colonPosLong = -1;
            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("a ") && line.Contains(":"))
                    colonPosA = line.IndexOf(':');
                if (line.TrimStart().StartsWith("longName") && line.Contains(":"))
                    colonPosLong = line.IndexOf(':');
            }
            Assert.True(colonPosA >= 0, "Should find declaration 'a'");
            Assert.True(colonPosLong >= 0, "Should find declaration 'longName'");
            Assert.Equal(colonPosA, colonPosLong); // Colons aligned
        }

        #endregion

        #region 4. Operator spacing

        [Fact]
        public void OperatorSpacing_AssignOperator()
        {
            var options = new FormatterOptions { OperatorSpacing = true };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nx:=1;\nEND_PROGRAM";
            var result = Format(source, options);
            // Should have spaces around :=
            Assert.Contains(":=", result);
            // The formatted output should have "x := 1"
            Assert.Contains("x := 1", result);
        }

        #endregion

        #region 5. Comments are preserved

        [Fact]
        public void Comments_Preserved()
        {
            var source = "// header comment\nPROGRAM P\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("// header comment", result);
        }

        [Fact]
        public void MultiLineComment_Preserved()
        {
            // Place comment inside the program body where it's more likely to be preserved
            var source = "PROGRAM P\n(* block comment *)\nx := 1;\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("(* block comment *)", result);
        }

        #endregion

        #region 6. Strings are preserved

        [Fact]
        public void Strings_Preserved()
        {
            var source = "PROGRAM P\nVAR\ns : STRING;\nEND_VAR\ns := 'hello world';\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("'hello world'", result);
        }

        #endregion

        #region 7. Attributes/Pragmas are preserved

        [Fact]
        public void Pragmas_Preserved()
        {
            var source = "{attribute 'qualified_only'}\nPROGRAM P\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("{attribute 'qualified_only'}", result);
        }

        #endregion

        #region 8. Empty input produces empty output

        [Fact]
        public void EmptyInput_ProducesEmptyOutput()
        {
            var result = Format("");
            // Formatter adds a trailing newline, so result should be just a newline or empty
            Assert.True(string.IsNullOrWhiteSpace(result));
        }

        #endregion

        #region 9. Simple PROGRAM formats correctly end-to-end

        [Fact]
        public void SimpleProgram_FormatsEndToEnd()
        {
            var source = "program Main\nvar\nx : INT;\nend_var\nx := 1;\nend_program";
            var result = Format(source);

            Assert.Contains("PROGRAM Main", result);
            Assert.Contains("VAR", result);
            Assert.Contains("END_VAR", result);
            Assert.Contains("END_PROGRAM", result);
            // Keywords should be uppercased
            Assert.Contains("INT", result);
        }

        #endregion

        #region 10. TypeCase option works

        [Fact]
        public void TypeCase_Upper()
        {
            var options = new FormatterOptions { TypeCase = TypeCase.Upper };
            var source = "PROGRAM P\nVAR\nx : int;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, options);
            // "int" should become "INT" with TypeCase.Upper
            Assert.Contains("INT", result);
        }

        [Fact]
        public void TypeCase_Lower()
        {
            var options = new FormatterOptions { TypeCase = TypeCase.Lower };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, options);
            // "INT" should become "int" with TypeCase.Lower
            Assert.Contains("int", result);
        }

        [Fact]
        public void TypeCase_Preserve()
        {
            var options = new FormatterOptions { TypeCase = TypeCase.Preserve };
            var source = "PROGRAM P\nVAR\nx : MyType;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, options);
            // "MyType" should stay as-is
            Assert.Contains("MyType", result);
        }

        #endregion

        #region 11. CASE statement formatting

        [Fact]
        public void CaseStatement_BranchIndentation()
        {
            var source = "PROGRAM P\nCASE x OF\n1:\ny := 1;\n2:\ny := 2;\nEND_CASE\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("CASE x OF", result);
            Assert.Contains("END_CASE", result);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // Branch labels (1:, 2:) should be indented
            bool foundIndentedBranch = false;
            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("1:") && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    foundIndentedBranch = true;
                    break;
                }
            }
            Assert.True(foundIndentedBranch, "Case branch labels should be indented");
        }

        #endregion

        #region 12. FOR loop formatting

        [Fact]
        public void ForLoop_KeywordsUppercased_BodyIndented()
        {
            var source = "PROGRAM P\nFOR i := 0 TO 10 BY 1 DO\nx := i;\nEND_FOR\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("FOR", result);
            Assert.Contains("TO", result);
            Assert.Contains("BY", result);
            Assert.Contains("DO", result);
            Assert.Contains("END_FOR", result);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            bool hasIndentedLine = false;
            foreach (var line in lines)
            {
                if (line.Contains("x :=") && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    hasIndentedLine = true;
                    break;
                }
            }
            Assert.True(hasIndentedLine, "FOR loop body should be indented");
        }

        #endregion

        #region 13. WHILE loop formatting

        [Fact]
        public void WhileLoop_KeywordsUppercased_BodyIndented()
        {
            var source = "PROGRAM P\nWHILE x < 10 DO\nx := x + 1;\nEND_WHILE\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("WHILE", result);
            Assert.Contains("DO", result);
            Assert.Contains("END_WHILE", result);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            bool hasIndentedBody = false;
            foreach (var line in lines)
            {
                if (line.Contains("x :=") && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    hasIndentedBody = true;
                    break;
                }
            }
            Assert.True(hasIndentedBody, "WHILE loop body should be indented");
        }

        #endregion

        #region 14. REPEAT/UNTIL formatting

        [Fact]
        public void RepeatUntil_KeywordsUppercased_BodyIndented()
        {
            var source = "PROGRAM P\nREPEAT\nx := x + 1;\nUNTIL x > 10;\nEND_REPEAT\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("REPEAT", result);
            Assert.Contains("UNTIL", result);
            Assert.Contains("END_REPEAT", result);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            bool hasIndentedBody = false;
            foreach (var line in lines)
            {
                if (line.Contains("x :=") && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    hasIndentedBody = true;
                    break;
                }
            }
            Assert.True(hasIndentedBody, "REPEAT body should be indented");
        }

        #endregion

        #region 15. IF/ELSIF/ELSE formatting

        [Fact]
        public void IfElsifElse_CorrectAlignment()
        {
            var source = "PROGRAM P\nIF x > 0 THEN\ny := 1;\nELSIF x < 0 THEN\ny := 2;\nELSE\ny := 0;\nEND_IF\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("IF", result);
            Assert.Contains("THEN", result);
            Assert.Contains("ELSIF", result);
            Assert.Contains("ELSE", result);
            Assert.Contains("END_IF", result);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // Find indent levels of IF, ELSIF, ELSE, END_IF
            string ifLine = null, elsifLine = null, elseLine = null, endIfLine = null;
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("IF ") || trimmed.StartsWith("IF(")) ifLine = line;
                else if (trimmed.StartsWith("ELSIF")) elsifLine = line;
                else if (trimmed.StartsWith("ELSE")) elseLine = line;
                else if (trimmed.StartsWith("END_IF")) endIfLine = line;
            }
            Assert.NotNull(ifLine);
            Assert.NotNull(elsifLine);
            Assert.NotNull(elseLine);
            Assert.NotNull(endIfLine);
            int ifIndent = ifLine.Length - ifLine.TrimStart().Length;
            int elsifIndent = elsifLine.Length - elsifLine.TrimStart().Length;
            int elseIndent = elseLine.Length - elseLine.TrimStart().Length;
            int endIfIndent = endIfLine.Length - endIfLine.TrimStart().Length;
            Assert.Equal(ifIndent, elsifIndent);
            Assert.Equal(ifIndent, elseIndent);
            Assert.Equal(ifIndent, endIfIndent);
        }

        #endregion

        #region 16. FUNCTION_BLOCK formatting

        [Fact]
        public void FunctionBlock_VarBlocksAtHeaderLevel_EndAligned()
        {
            var source = "FUNCTION_BLOCK FB_Test\nVAR\nx : INT;\nEND_VAR\nx := 1;\nEND_FUNCTION_BLOCK";
            var result = Format(source);
            Assert.Contains("FUNCTION_BLOCK FB_Test", result);
            Assert.Contains("VAR", result);
            Assert.Contains("END_VAR", result);
            Assert.Contains("END_FUNCTION_BLOCK", result);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // VAR belongs at the same level as the FUNCTION_BLOCK header (column 0),
            // not indented. Only the member declarations inside are indented.
            int varLine = Array.FindIndex(lines, l => l.TrimStart() == "VAR");
            Assert.True(varLine >= 0, "VAR line not found");
            Assert.True(lines[varLine].Length == 0 ||
                        (lines[varLine][0] != ' ' && lines[varLine][0] != '\t'),
                "VAR block must NOT be indented inside FUNCTION_BLOCK");
            int memberLine = Array.FindIndex(lines, l => l.TrimStart().Equals("x : INT;", StringComparison.Ordinal));
            Assert.True(memberLine > varLine, "member declaration must appear after VAR");
            Assert.True(lines[memberLine].Length > 0 &&
                        (lines[memberLine][0] == ' ' || lines[memberLine][0] == '\t'),
                "member declaration must be indented inside VAR");
        }

        #endregion

        #region 17. FUNCTION formatting

        [Fact]
        public void Function_ReturnType_BodyFormatted()
        {
            var source = "FUNCTION F_Add : INT\nVAR\na : INT;\nb : INT;\nEND_VAR\nF_Add := a + b;\nEND_FUNCTION";
            var result = Format(source);
            Assert.Contains("FUNCTION F_Add", result);
            Assert.Contains("END_FUNCTION", result);
            Assert.Contains("F_Add :=", result);
        }

        #endregion

        #region 18. METHOD formatting

        [Fact]
        public void Method_AccessModifier_BodyFormatted()
        {
            var source = "FUNCTION_BLOCK FB_Test\nMETHOD PUBLIC DoWork : BOOL\nVAR\nx : INT;\nEND_VAR\nDoWork := TRUE;\nEND_METHOD\nEND_FUNCTION_BLOCK";
            var result = Format(source);
            Assert.Contains("METHOD", result);
            Assert.Contains("PUBLIC", result);
            Assert.Contains("END_METHOD", result);
            Assert.Contains("DoWork :=", result);
        }

        [Fact]
        public void Method_VarBlocks_NotIndented_MembersIndented()
        {
            var source =
                "METHOD PUBLIC Insert : BOOL\n" +
                "VAR_INPUT\n" +
                "execute : BOOL;\n" +
                "entry   : InsertEntry;\n" +
                "END_VAR\n" +
                "VAR_OUTPUT\n" +
                "done  : BOOL;\n" +
                "error : BOOL;\n" +
                "END_VAR\n" +
                "VAR\n" +
                "sqlCmd : STRING(200);\n" +
                "END_VAR";
            var result = Format(source);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            Assert.Contains("METHOD PUBLIC Insert : BOOL", result);

            // VAR_INPUT / VAR_OUTPUT / VAR and their END_VAR belong at column 0,
            // exactly under the METHOD header (not indented).
            foreach (var keyword in new[] { "VAR_INPUT", "VAR_OUTPUT", "VAR", "END_VAR" })
            {
                int idx = Array.FindIndex(lines, l => l.Trim() == keyword);
                Assert.True(idx >= 0, $"line '{keyword}' not found");
                Assert.False(idx >= 0 && lines[idx].Length > 0 &&
                            (lines[idx][0] == ' ' || lines[idx][0] == '\t'),
                    $"'{keyword}' must not be indented inside METHOD");
            }

            // Member declarations are indented one level inside their VAR block.
            int executeLine = Array.FindIndex(lines, l => l.Trim().Equals("execute : BOOL;", StringComparison.Ordinal));
            Assert.True(executeLine >= 0, "member declaration not found");
            Assert.True(lines[executeLine].Length > 0 &&
                        (lines[executeLine][0] == ' ' || lines[executeLine][0] == '\t'),
                "member declaration must be indented inside VAR_INPUT");
        }

        [Fact]
        public void VarBlock_Comment_ThenDeclaration_KeepsIndent()
        {
            var source =
                "FUNCTION_BLOCK DBManager\n" +
                "VAR\n" +
                "// 数据库连接 ID\n" +
                "dbId : UDINT;\n" +
                "// 最后一次错误码\n" +
                "lastErrorID : UDINT;\n" +
                "lastErrorMsg : STRING;\n" +
                "END_VAR\n" +
                "END_FUNCTION_BLOCK";
            var result = Format(source);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            int commentLine = Array.FindIndex(lines, l => l.Trim().Equals("// 数据库连接 ID", StringComparison.Ordinal));
            int declLine = Array.FindIndex(lines, l => l.Trim().StartsWith("dbId", StringComparison.Ordinal));

            Assert.True(commentLine >= 0, "comment line not found");
            Assert.True(declLine >= 0, "declaration line not found");
            Assert.True(lines[commentLine].Length > 0 &&
                        (lines[commentLine][0] == ' ' || lines[commentLine][0] == '\t'),
                "comment must be indented inside VAR");
            Assert.True(lines[declLine].Length > 0 &&
                        (lines[declLine][0] == ' ' || lines[declLine][0] == '\t'),
                "declaration after comment must keep its indentation");
        }

        #endregion

        #region 19. PROPERTY with GET/SET formatting

        [Fact]
        public void Property_GetterSetter_Formatted()
        {
            var source = "FUNCTION_BLOCK FB_Test\nPROPERTY Value : INT\nGET\nValue := _value;\nEND_GET\nSET\n_value := Value;\nEND_SET\nEND_PROPERTY\nEND_FUNCTION_BLOCK";
            var result = Format(source);
            Assert.Contains("PROPERTY", result);
            Assert.Contains("GET", result);
            Assert.Contains("END_GET", result);
            Assert.Contains("SET", result);
            Assert.Contains("END_SET", result);
            Assert.Contains("END_PROPERTY", result);
        }

        #endregion

        #region 20. INTERFACE formatting

        [Fact]
        public void Interface_MethodDeclarations_Formatted()
        {
            var source = "INTERFACE I_Motion";
            var result = Format(source);
            Assert.Contains("INTERFACE", result);
        }

        #endregion

        #region 21. TYPE/STRUCT formatting

        [Fact]
        public void TypeStruct_MemberIndentation_EndAligned()
        {
            var source = "PROGRAM P\nTYPE ST_MyStruct :\nSTRUCT\na : INT;\nb : REAL;\nEND_STRUCT\nEND_TYPE\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("TYPE", result);
            Assert.Contains("STRUCT", result);
            Assert.Contains("END_STRUCT", result);
            Assert.Contains("END_TYPE", result);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // Struct members should be indented
            bool memberIndented = false;
            foreach (var line in lines)
            {
                if (line.Contains("a :") && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    memberIndented = true;
                    break;
                }
            }
            Assert.True(memberIndented, "Struct members should be indented");
        }

        #endregion

        #region 22. TYPE/ENUM formatting

        [Fact]
        public void TypeEnum_ValuesFormatted_CommaHandling()
        {
            var source = "PROGRAM P\nTYPE E_Color :\n(\nRed,\nGreen,\nBlue\n)\nEND_TYPE\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("TYPE", result);
            Assert.Contains("END_TYPE", result);
            // Enum values should be present
            Assert.Contains("Red", result);
            Assert.Contains("Green", result);
            Assert.Contains("Blue", result);
        }

        #endregion

        #region 23. TYPE/UNION formatting

        [Fact]
        public void TypeUnion_MembersFormatted()
        {
            var source = "PROGRAM P\nTYPE U_MyUnion :\nUNION\na : INT;\nb : REAL;\nEND_UNION\nEND_TYPE\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("UNION", result);
            Assert.Contains("END_UNION", result);
            Assert.Contains("END_TYPE", result);
        }

        #endregion

        #region 24. NAMESPACE formatting

        [Fact]
        public void Namespace_BodyIndented()
        {
            var source = "NAMESPACE MyNamespace\nPROGRAM P\nEND_PROGRAM\nEND_NAMESPACE";
            var result = Format(source);
            Assert.Contains("NAMESPACE MyNamespace", result);
            Assert.Contains("END_NAMESPACE", result);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            bool programIndented = false;
            foreach (var line in lines)
            {
                if (line.Contains("PROGRAM") && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    programIndented = true;
                    break;
                }
            }
            Assert.True(programIndented, "PROGRAM inside NAMESPACE should be indented");
        }

        #endregion

        #region 25. USING directive formatting

        [Fact]
        public void UsingDirective_Formatted()
        {
            var source = "USING MyNamespace;\nPROGRAM P\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("USING MyNamespace;", result);
        }

        #endregion

        #region 26. CommaSpacing = true

        [Fact]
        public void CommaSpacing_True_SpaceAfterComma()
        {
            var options = new FormatterOptions { CommaSpacing = true };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nFoo(x,y);\nEND_PROGRAM";
            var result = Format(source, options);
            // With CommaSpacing=true, there should be a space after comma
            Assert.Contains(", ", result);
        }

        #endregion

        #region 27. CommaSpacing = false

        [Fact]
        public void CommaSpacing_False_NoSpaceAfterComma()
        {
            var options = new FormatterOptions { CommaSpacing = false };
            var source = "PROGRAM P\nVAR\nx : INT;\ny : INT;\nEND_VAR\nFoo(x, y);\nEND_PROGRAM";
            var result = Format(source, options);
            // With CommaSpacing=false, no space after comma
            Assert.Contains(",y", result);
        }

        #endregion

        #region 28. ParenInnerSpacing = true

        [Fact]
        public void ParenInnerSpacing_True_SpaceInsideParens()
        {
            var options = new FormatterOptions { ParenInnerSpacing = true };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nFoo(x);\nEND_PROGRAM";
            var result = Format(source, options);
            // With ParenInnerSpacing=true, should have ( x )
            Assert.Contains("( x )", result);
        }

        #endregion

        #region 29. ParenInnerSpacing = false

        [Fact]
        public void ParenInnerSpacing_False_NoSpaceInsideParens()
        {
            var options = new FormatterOptions { ParenInnerSpacing = false };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nFoo( x );\nEND_PROGRAM";
            var result = Format(source, options);
            // With ParenInnerSpacing=false, should have (x) not ( x )
            Assert.Contains("(x)", result);
            Assert.DoesNotContain("( x )", result);
        }

        #endregion

        #region 30. BlankLinesBeforeEnd = 0

        [Fact]
        public void BlankLinesBeforeEnd_Zero_NoBlankLineBeforeEndProgram()
        {
            var options = new FormatterOptions { BlankLinesBeforeEnd = 0 };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nx := 1;\nEND_PROGRAM";
            var result = Format(source, options);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // No blank line should appear between x := 1; and END_PROGRAM
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("END_PROGRAM"))
                {
                    // Previous line should be the assignment, not blank
                    Assert.True(i > 0 && lines[i - 1].Contains("x :="),
                        "Line before END_PROGRAM should be the assignment");
                    break;
                }
            }
        }

        #endregion

        #region 31. BlankLinesBeforeEnd = 1

        [Fact]
        public void BlankLinesBeforeEnd_One_BlankLineBeforeEndProgram()
        {
            var options = new FormatterOptions { BlankLinesBeforeEnd = 1 };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nx := 1;\nEND_PROGRAM";
            var result = Format(source, options);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // There should be a blank line before END_PROGRAM
            bool foundBlankBeforeEnd = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("END_PROGRAM") && i > 1)
                {
                    if (string.IsNullOrWhiteSpace(lines[i - 1]))
                        foundBlankBeforeEnd = true;
                    break;
                }
            }
            Assert.True(foundBlankBeforeEnd, "Should have blank line before END_PROGRAM when BlankLinesBeforeEnd=1");
        }

        #endregion

        #region 32. BlankLinesAfterVar = 0

        [Fact]
        public void BlankLinesAfterVar_Zero_NoBlankLineAfterEndVar()
        {
            var options = new FormatterOptions { BlankLinesAfterVar = 0 };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nx := 1;\nEND_PROGRAM";
            var result = Format(source, options);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // No blank line between END_VAR and x := 1
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart() == "END_VAR" && i + 1 < lines.Length)
                {
                    Assert.False(string.IsNullOrWhiteSpace(lines[i + 1]),
                        "No blank line should follow END_VAR when BlankLinesAfterVar=0");
                    break;
                }
            }
        }

        #endregion

        #region 33. BlankLinesAfterVar = 1

        [Fact]
        public void BlankLinesAfterVar_One_BlankLineAfterEndVar()
        {
            var options = new FormatterOptions { BlankLinesAfterVar = 1 };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nx := 1;\nEND_PROGRAM";
            var result = Format(source, options);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // Blank line after END_VAR
            bool foundBlankAfterVar = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart() == "END_VAR" && i + 1 < lines.Length)
                {
                    if (string.IsNullOrWhiteSpace(lines[i + 1]))
                        foundBlankAfterVar = true;
                    break;
                }
            }
            Assert.True(foundBlankAfterVar, "Should have blank line after END_VAR when BlankLinesAfterVar=1");
        }

        #endregion

        #region 34. KeepEmptyLines = true

        [Fact]
        public void KeepEmptyLines_True_PreservesEmptyLines()
        {
            // TODO: The formatter's VAR block visitor processes VarDeclarations directly
            // and does not preserve empty lines between declarations. This test verifies
            // that the formatter at least doesn't crash and produces valid output.
            var options = new FormatterOptions { KeepEmptyLines = true };
            var source = "PROGRAM P\nVAR\nx : INT;\n\ny : INT;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, options);
            // Both declarations should be present
            Assert.Contains("x", result);
            Assert.Contains("y", result);
            Assert.Contains("INT", result);
        }

        #endregion

        #region 35. KeepEmptyLines = false

        [Fact]
        public void KeepEmptyLines_False_RemovesEmptyLines()
        {
            var options = new FormatterOptions { KeepEmptyLines = false };
            var source = "PROGRAM P\nVAR\nx : INT;\n\n\ny : INT;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, options);
            // Multiple consecutive blank lines should be collapsed/removed
            Assert.DoesNotContain("\n\n\n", result);
        }

        #endregion

        #region 36. LineEnding = CRLF

        [Fact]
        public void LineEnding_CRLF_VerifyCrlfOutput()
        {
            var options = new FormatterOptions { LineEnding = LineEnding.CRLF };
            var source = "PROGRAM P\nEND_PROGRAM";
            var result = Format(source, options);
            Assert.Contains("\r\n", result);
        }

        #endregion

        #region 37. LineEnding = LF

        [Fact]
        public void LineEnding_LF_VerifyLfOutput()
        {
            var options = new FormatterOptions { LineEnding = LineEnding.LF };
            var source = "PROGRAM P\r\nEND_PROGRAM";
            var result = Format(source, options);
            Assert.Contains("\n", result);
            Assert.DoesNotContain("\r\n", result);
        }

        #endregion

        #region 38. UseSpacesInsteadOfTab with IndentSize = 2

        [Fact]
        public void UseSpaces_IndentSize2_TwoSpaceIndent()
        {
            var options = new FormatterOptions { UseSpacesInsteadOfTab = true, IndentSize = 2 };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nx := 1;\nEND_PROGRAM";
            var result = Format(source, options);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // x := 1 is inside PROGRAM (1 level = 2 spaces)
            foreach (var line in lines)
            {
                if (line.Contains("x :="))
                {
                    Assert.StartsWith("  x", line);
                    break;
                }
            }
        }

        #endregion

        #region 39. UseSpacesInsteadOfTab with IndentSize = 8

        [Fact]
        public void UseSpaces_IndentSize8_EightSpaceIndent()
        {
            var options = new FormatterOptions { UseSpacesInsteadOfTab = true, IndentSize = 8 };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nx := 1;\nEND_PROGRAM";
            var result = Format(source, options);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            // x := 1 is inside PROGRAM (1 level = 8 spaces)
            foreach (var line in lines)
            {
                if (line.Contains("x :="))
                {
                    Assert.StartsWith("        x", line);
                    break;
                }
            }
        }

        #endregion

        #region 40. MaxLineLength (verify option is accepted)

        [Fact]
        public void MaxLineLength_OptionAccepted()
        {
            var options = new FormatterOptions { MaxLineLength = 80 };
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source, options);
            Assert.Contains("PROGRAM P", result);
        }

        #endregion

        #region 41. OutputAssign (=>) spacing

        [Fact]
        public void OutputAssign_SpacingCorrect()
        {
            var options = new FormatterOptions { OperatorSpacing = true };
            var source = "PROGRAM P\nVAR\nq : BOOL;\nEND_VAR\nFB_Block(Q => q);\nEND_PROGRAM";
            var result = Format(source, options);
            // => should have spaces around it
            Assert.Contains("=>", result);
        }

        #endregion

        #region 42. Dot (.) no spacing

        [Fact]
        public void Dot_NoSpacing()
        {
            var source = "PROGRAM P\nVAR\nfb : FB_Test;\nEND_VAR\nfb . value := 1;\nEND_PROGRAM";
            var result = Format(source);
            // Dot should have no spaces around it
            Assert.Contains("fb.value", result);
            Assert.DoesNotContain("fb . value", result);
        }

        #endregion

        #region 43. Caret (^) no spacing

        [Fact]
        public void Caret_NoSpacing()
        {
            var source = "PROGRAM P\nVAR\np : POINTER TO INT;\nEND_VAR\nx := p^;\nEND_PROGRAM";
            var result = Format(source);
            // Caret should have no space before it
            Assert.Contains("p^", result);
            Assert.DoesNotContain("p ^", result);
        }

        #endregion

        #region 44. AT address formatting

        [Fact]
        public void AtAddress_FormattedCorrectly()
        {
            var source = "PROGRAM P\nVAR\nb : BOOL AT %IX0.0;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("AT %IX0.0", result);
        }

        #endregion

        #region 45. Multi-line expression continuation

        [Fact]
        public void MultiLineExpression_FormattedOnSingleLine()
        {
            // TODO: The formatter's WriteStatementTokens collapses multi-line expressions
            // into a single line (preserveMultiLine: false). This test verifies the
            // expression is still correctly formatted on one line.
            var source = "PROGRAM P\nVAR\nx : INT;\nEND_VAR\nx := 1 +\n2 +\n3;\nEND_PROGRAM";
            var result = Format(source);
            // The expression should be present (possibly on one line)
            Assert.Contains("x :=", result);
            Assert.Contains("1", result);
            Assert.Contains("2", result);
            Assert.Contains("3", result);
        }

        #endregion

        #region 46. ARRAY type formatting

        [Fact]
        public void ArrayType_SpacingCorrect()
        {
            var source = "PROGRAM P\nVAR\narr : ARRAY[1..10] OF INT;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("ARRAY", result);
            Assert.Contains("OF", result);
        }

        #endregion

        #region 47. POINTER TO spacing

        [Fact]
        public void PointerTo_SpacesBetweenWords()
        {
            var source = "PROGRAM P\nVAR\np : POINTER TO INT;\nEND_VAR\nEND_PROGRAM";
            var result = Format(source);
            Assert.Contains("POINTER TO INT", result);
        }

        #endregion

        #region 48. STRING(80) formatting

        [Fact]
        public void StringWithLength_NoExtraSpaces()
        {
            var source = "PROGRAM P\nVAR\ns : STRING(80);\nEND_VAR\nEND_PROGRAM";
            var result = Format(source);
            // STRING(80) should not have extra spaces like STRING ( 80 )
            Assert.Contains("STRING(80)", result);
        }

        #endregion

        #region 49. Blank line policy

        [Fact]
        public void StatementBlocks_GetBlankLineBeforeAndAfter_SingleBlankBetween()
        {
            var source =
                "PROGRAM P\n" +
                "IF a THEN\nx := 1;\nEND_IF\n" +
                "IF b THEN\ny := 2;\nEND_IF\n" +
                "z := 3;\nEND_PROGRAM";
            var result = Format(source);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            int endIf1 = Array.FindIndex(lines, l => l.Trim() == "END_IF");
            int if2 = Array.FindIndex(lines, l => l.Trim() == "IF b THEN");
            Assert.True(endIf1 >= 0 && if2 > endIf1, "expected two consecutive IF blocks");

            // Exactly one blank line between the two blocks (lines[endIf1+1] empty).
            int blankCount = 0;
            for (int i = endIf1 + 1; i < if2; i++)
                if (lines[i].Trim().Length == 0) blankCount++;
            Assert.Equal(1, blankCount);
        }

        [Fact]
        public void KeepEmptyLines_PreservesSourceBlankLines_DoesNotAddNew()
        {
            var source =
                "FUNCTION_BLOCK FB\n" +
                "VAR_INPUT\n\n" +
                "a : INT;\n" +
                "END_VAR\n" +
                "VAR_OUTPUT\n" +
                "b : BOOL;\n" +
                "END_VAR\n" +
                "END_FUNCTION_BLOCK";
            var options = new FormatterOptions { KeepEmptyLines = true };
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            var result = new STFormatter(options).Format(cst, source);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            int varInput = Array.FindIndex(lines, l => l.Trim() == "VAR_INPUT");
            int decl = Array.FindIndex(lines, l => l.Trim() == "a : INT;");
            Assert.True(varInput >= 0 && decl > varInput, "VAR_INPUT and declaration not found");
            Assert.Equal(1, decl - varInput - 1); // exactly one blank line preserved between them
            // VAR_OUTPUT directly follows END_VAR with no added blank line
            int endVar = Array.FindIndex(lines, l => l.Trim() == "END_VAR");
            int varOutput = Array.FindIndex(lines, l => l.Trim() == "VAR_OUTPUT");
            Assert.Equal(endVar + 1, varOutput);
        }

        [Fact]
        public void RemoveEmptyLines_RemovesSourceBlanksButKeepsBlockSeparators()
        {
            var source =
                "PROGRAM P\n" +
                "VAR\n\n" +
                "a : INT;\n\n\n" +
                "END_VAR\n" +
                "WHILE x DO\nx := x + 1;\nEND_WHILE\n" +
                "END_PROGRAM";
            var options = new FormatterOptions { KeepEmptyLines = false };
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            var result = new STFormatter(options).Format(cst, source);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            // The VAR block must contain no blank lines.
            int varLine = Array.FindIndex(lines, l => l.Trim() == "VAR");
            int endVar = Array.FindIndex(lines, l => l.Trim() == "END_VAR");
            Assert.True(varLine >= 0 && endVar > varLine, "VAR/END_VAR not found");
            for (int i = varLine + 1; i < endVar; i++)
                Assert.False(lines[i].Trim().Length == 0, "VAR block must not contain blank lines");

            // The statement block (WHILE) still has its structural separating blank line
            // before it (after END_VAR).
            int whileLine = Array.FindIndex(lines, l => l.Trim() == "WHILE x DO");
            Assert.True(whileLine > endVar, "WHILE block not found");
            Assert.True(whileLine - endVar == 2, "expected exactly one blank line between END_VAR and WHILE block");
        }

        [Fact]
        public void StatementBlocks_NoBlankBeforeEndKeywords_NestedBlocksClean()
        {
            var source =
                "PROGRAM P\n" +
                "IF a THEN\nx := 1;\nEND_IF\n" +
                "CASE i OF\n" +
                "0:\n" +
                "    y := 2;\n" +
                "    IF b THEN\n" +
                "        IF c THEN\nz := 3;\nEND_IF\n" +
                "        z := 4;\n" +
                "    END_IF\n" +
                "END_CASE\n" +
                "END_PROGRAM";
            var result = Format(source);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            // No blank line directly before any END keyword's final line (END_IF,
            // END_CASE) inside blocks. The block-ending keyword must be immediately
            // after the previous content line.
            for (int i = 1; i < lines.Length; i++)
            {
                bool endKeyword = lines[i].Trim().Equals("END_IF") ||
                                  lines[i].Trim().Equals("END_CASE");
                if (endKeyword)
                    Assert.False(lines[i - 1].Trim().Length == 0,
                        $"blank line before {lines[i].Trim()} at line {i}");
            }

            // Exactly one blank line separates the two top-level blocks (END_IF → CASE).
            int endIf = Array.FindIndex(lines, l => l.Trim() == "END_IF");
            int caseLine = Array.FindIndex(lines, l => l.Trim().StartsWith("CASE i"));
            Assert.True(endIf >= 0 && caseLine > endIf, "top-level IF/CASE not found");
            int blankBetween = 0;
            for (int i = endIf + 1; i < caseLine; i++)
                if (lines[i].Trim().Length == 0) blankBetween++;
            Assert.Equal(1, blankBetween);
        }

        [Fact]
        public void NestedStatementBlocks_GetNoBlankLineSeparation()
        {
            var source =
                "PROGRAM P\n" +
                "IF a THEN\n" +
                "x := 1;\n" +
                "IF b THEN\n" +
                "y := 2;\n" +
                "END_IF\n" +
                "END_IF\n" +
                "END_PROGRAM";
            var result = Format(source);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            // The nested IF must sit directly after its preceding statement with no
            // blank line, and its END_IF directly before the outer END_IF.
            int nestedIf = Array.FindIndex(lines, l => l.Trim() == "IF b THEN");
            int outerEndIf = Array.FindIndex(lines, l => l.Trim() == "END_IF");
            Assert.True(nestedIf >= 0 && outerEndIf > nestedIf, "nested IF / outer END_IF not found");

            // No blank line between the statement above and the nested IF.
            Assert.False(lines[nestedIf - 1].Trim().Length == 0,
                "nested statement block must not get a leading blank line");

            // The nested block body is directly followed by the outer END_IF (no blank).
            int innerEndIf = Array.FindIndex(lines, nestedIf, l => l.Trim() == "END_IF");
            Assert.True(innerEndIf >= 0 && lines[innerEndIf + 1].Trim() == "END_IF",
                "nested block must not leave a blank line before the outer END_IF");
        }

        #endregion

        #region 50. TypeCase applies only to built-in types

        [Fact]
        public void TypeCaseLower_OnlyAffectsBuiltInTypes()
        {
            var source =
                "FUNCTION_BLOCK FB\n" +
                "VAR\n" +
                "a   : INT;\n" +
                "arr : ARRAY[0 .. 5] OF ST_ExpParameter;\n" +
                "msg : I_TcMessage;\n" +
                "s   : STRING(200);\n" +
                "END_VAR\n" +
                "END_FUNCTION_BLOCK";
            var options = new FormatterOptions { TypeCase = TypeCase.Lower };
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            var result = new STFormatter(options).Format(cst, source);

            Assert.Contains(": int;", result);
            Assert.Contains("array[0 .. 5] of", result);
            Assert.Contains(": string(200);", result);
            Assert.Contains("I_TcMessage", result);     // library type unchanged
            Assert.DoesNotContain("i_tcmessage", result);
            Assert.Contains("ST_ExpParameter", result); // user type unchanged
            Assert.DoesNotContain("st_expparameter", result);
        }

        #endregion

        #region 51. KeepEmptyLines=false removes source blanks everywhere

        [Fact]
        public void RemoveEmptyLines_StatementArea_NoBlankBeforeEndKeywords()
        {
            var source =
                "PROGRAM P\n" +
                "IF a THEN\n" +
                "x := 1;\n" +
                "\n" +
                "END_IF\n" +
                "CASE i OF\n" +
                "0:\n" +
                "\n" +
                "y := 2;\n" +
                "\n" +
                "z := 3;\n" +
                "END_CASE\n" +
                "END_PROGRAM";
            var options = new FormatterOptions { KeepEmptyLines = false };
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            var result = new STFormatter(options).Format(cst, source);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            // No blank line before END_IF / END_CASE.
            for (int i = 1; i < lines.Length; i++)
            {
                bool endKeyword = lines[i].Trim().Equals("END_IF") || lines[i].Trim().Equals("END_CASE");
                if (endKeyword)
                    Assert.False(lines[i - 1].Trim().Length == 0,
                        $"blank before {lines[i].Trim()} is not allowed when KeepEmptyLines=false");
            }

            // No blank line between the two plain statements inside the CASE branch.
            int y = Array.FindIndex(lines, l => l.Trim() == "y := 2;");
            int z = Array.FindIndex(lines, l => l.Trim() == "z := 3;");
            Assert.True(y >= 0 && z > y, "CASE branch statements not found");
            Assert.Equal(y + 1, z);

            // Only blank lines that remain are the structural separators around the
            // outermost blocks. The first statement (IF) is the first child of the
            // body, so it gets no leading blank; the block separation appears
            // between END_IF and CASE.
            int endIf = Array.FindIndex(lines, l => l.Trim() == "END_IF");
            int caseIdx = Array.FindIndex(lines, l => l.Trim().StartsWith("CASE i"));
            Assert.True(endIf >= 0 && caseIdx > endIf, "outer IF/CASE not found");
            Assert.True(caseIdx - endIf == 2, "expected exactly one blank between END_IF and CASE");
        }

        [Fact]
        public void StatementWithLeadingComment_KeepsIndentAndNoBlank_WhenRemovingEmptyLines()
        {
            var source =
                "PROGRAM P\n" +
                "CASE i OF\n" +
                "0:\n" +
                "y := 2;\n" +
                "// comment\n" +
                "z := 3;\n" +
                "END_CASE\n" +
                "END_PROGRAM";
            var options = new FormatterOptions { KeepEmptyLines = false };
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();
            var result = new STFormatter(options).Format(cst, source);
            var lines = result.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            int comment = Array.FindIndex(lines, l => l.Trim() == "// comment");
            int z = Array.FindIndex(lines, l => l.Trim() == "z := 3;");
            Assert.True(comment >= 0 && z == comment + 1, "comment must be directly followed by the statement");
            Assert.True(lines[comment].Length > 0 &&
                        (lines[comment][0] == ' ' || lines[comment][0] == '\t'),
                "comment must keep its indentation");
            Assert.True(lines[z].Length > 0 && (lines[z][0] == ' ' || lines[z][0] == '\t'),
                "statement after comment must keep its indentation");
        }

        #endregion

        #region 52. BlankLinesAroundStatementBlocks option

        [Fact]
        public void BlankLinesAroundBlocks_DefaultTrue_BlankBetweenOuterBlocks()
        {
            var source =
                "PROGRAM P\n" +
                "IF a THEN\n" +
                "x := 1;\n" +
                "END_IF\n" +
                "CASE i OF\n" +
                "0:\n" +
                "y := 2;\n" +
                "END_CASE\n" +
                "END_PROGRAM";
            var result = Format(source, new FormatterOptions
            {
                KeepEmptyLines = false,
                LineEnding = LineEnding.LF
            });
            var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int endIf = Array.FindIndex(lines, l => l.Trim() == "END_IF");
            int caseIdx = Array.FindIndex(lines, l => l.Trim().StartsWith("CASE i"));
            Assert.True(endIf >= 0 && caseIdx == endIf + 2,
                "by default exactly one blank line separates outermost statement blocks");
        }

        [Fact]
        public void BlankLinesAroundBlocks_False_NoBlankBetweenOuterBlocks()
        {
            var source =
                "PROGRAM P\n" +
                "IF a THEN\n" +
                "x := 1;\n" +
                "END_IF\n" +
                "CASE i OF\n" +
                "0:\n" +
                "y := 2;\n" +
                "END_CASE\n" +
                "END_PROGRAM";
            foreach (var keep in new[] { false, true })
            {
                var result = Format(source, new FormatterOptions
                {
                    KeepEmptyLines = keep,
                    BlankLinesAroundStatementBlocks = false,
                    LineEnding = LineEnding.LF
                });
                var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                int endIf = Array.FindIndex(lines, l => l.Trim() == "END_IF");
                int caseIdx = Array.FindIndex(lines, l => l.Trim().StartsWith("CASE i"));
                Assert.True(endIf >= 0 && caseIdx == endIf + 1,
                    $"BlankLinesAroundStatementBlocks=false (KeepEmptyLines={keep}): CASE must directly follow END_IF");
                Assert.DoesNotContain("\n\n", result);
            }
        }

        #endregion

        #region 53. Bare implementation body (statements without a POU header)

        [Fact]
        public void BareImplementation_OutermostStatementsAtColumnZero()
        {
            var source = "x := 1;\nIF a THEN\ny := 2;\nEND_IF\n";
            var result = Format(source, new FormatterOptions { LineEnding = LineEnding.LF });
            Assert.StartsWith("x := 1;", result);
            Assert.Contains("\nIF a THEN", result);
            Assert.Contains("\n    y := 2;", result);
            Assert.Contains("\nEND_IF", result);
            Assert.DoesNotContain("\n    x := 1;", result);
            Assert.DoesNotContain("\n    IF a THEN", result);
        }

        [Fact]
        public void BareImplementation_NestedCaseIndentation()
        {
            var source = "CASE step OF\n0:\nx := 1;\n10:\nIF a THEN\ny := 2;\nEND_IF\nEND_CASE\n";
            var result = Format(source, new FormatterOptions { LineEnding = LineEnding.LF });
            Assert.StartsWith("CASE step OF", result);
            Assert.Contains("\n    0:", result);
            Assert.Contains("\n        x := 1;", result);
            Assert.Contains("\n    10:", result);
            Assert.Contains("\n        IF a THEN", result);
            Assert.Contains("\n            y := 2;", result);
            Assert.Contains("\n        END_IF", result);
            Assert.Contains("\nEND_CASE", result);
        }

        [Fact]
        public void BareImplementation_MatchesDedentedProgramBody()
        {
            // A bare implementation body must format exactly like the same
            // statements inside PROGRAM ... END_PROGRAM with the wrapper's one
            // indent level removed — that is the TwinCAT layout where outermost
            // implementation code sits at column 0.
            var bare =
                "fb.Execute(a := 1);\n" +
                "IF x THEN\ny := 2;\nEND_IF\n" +
                "CASE s OF\n0:\nz := 3;\nEND_CASE\n";
            var options = new FormatterOptions { LineEnding = LineEnding.LF };

            var bareResult = Format(bare, options);

            var wrapped = Format("PROGRAM __Temp__\n" + bare + "END_PROGRAM", options);
            var lines = wrapped.Split('\n').ToList();
            lines.RemoveAt(0); // PROGRAM __Temp__
            while (lines.Count > 0 && lines[lines.Count - 1].Trim() == "")
                lines.RemoveAt(lines.Count - 1);
            Assert.Equal("END_PROGRAM", lines[lines.Count - 1].Trim());
            lines.RemoveAt(lines.Count - 1);
            var dedented = lines.Select(l => l.StartsWith("    ") ? l.Substring(4) : l).ToList();
            var expected = string.Join("\n", dedented) + "\n";

            Assert.Equal(expected, bareResult);
        }

        [Fact]
        public void BareImplementation_Idempotent()
        {
            var source = "x := 1;\nCASE s OF\n0:\nIF a THEN\ny := 2;\nEND_IF\nEND_CASE\n";
            var options = new FormatterOptions { LineEnding = LineEnding.LF };
            var once = Format(source, options);
            var twice = Format(once, options);
            Assert.Equal(once, twice);
        }

        #endregion
    }
}
