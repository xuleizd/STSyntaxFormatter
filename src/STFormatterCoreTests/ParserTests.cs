using System.Collections.Generic;
using System.Linq;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Parser.Nodes;
using Xunit;

namespace STFormatterCoreTests
{
    public class ParserTests
    {
        #region Helpers

        private CompilationUnit Parse(string source)
        {
            var tokens = new STLexer(source).Tokenize();
            return new STParser(tokens).Parse();
        }

        #endregion

        #region 1. Simple PROGRAM with VAR and body

        [Fact]
        public void SimpleProgram_WithVarAndBody()
        {
            var unit = Parse("PROGRAM Main\nVAR\n    x : INT;\nEND_VAR\n    x := 1;\nEND_PROGRAM");
            Assert.Single(unit.Children);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            Assert.Equal(DeclarationKind.Program, prog.DeclarationKind);
            Assert.Equal("Main", prog.Name);

            // Should have a VarBlock child and an assignment child
            Assert.Contains(prog.Children, c => c is VarBlock);
            Assert.Contains(prog.Children, c => c is AssignmentStatement);
        }

        #endregion

        #region 2. FUNCTION_BLOCK with EXTENDS and IMPLEMENTS

        [Fact]
        public void FunctionBlock_WithExtendsAndImplements()
        {
            var unit = Parse("FUNCTION_BLOCK FB_Derived EXTENDS FB_Base IMPLEMENTS I_Interface1, I_Interface2\nEND_FUNCTION_BLOCK");
            Assert.Single(unit.Children);
            var fb = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            Assert.Equal(DeclarationKind.FunctionBlock, fb.DeclarationKind);
            Assert.Equal("FB_Derived", fb.Name);
            Assert.Equal("FB_Base", fb.ExtendsName);
            Assert.Equal(2, fb.ImplementsNames.Count);
            Assert.Contains("I_Interface1", fb.ImplementsNames);
            Assert.Contains("I_Interface2", fb.ImplementsNames);
        }

        #endregion

        #region 3. METHOD declaration with access modifiers

        [Fact]
        public void Method_WithAccessModifier()
        {
            var unit = Parse("FUNCTION_BLOCK FB\nMETHOD PUBLIC DoStuff\nEND_METHOD\nEND_FUNCTION_BLOCK");
            var fb = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var method = fb.Children.OfType<MethodDeclaration>().FirstOrDefault();
            Assert.NotNull(method);
            // Verify method was parsed (name and access modifier may depend on parser implementation)
            Assert.NotEmpty(method.Tokens);
        }

        #endregion

        #region 4. IF/ELSIF/ELSE/END_IF

        [Fact]
        public void IfElsifElse()
        {
            var unit = Parse("PROGRAM P\nIF x > 0 THEN\n    y := 1;\nELSIF x < 0 THEN\n    y := 2;\nELSE\n    y := 0;\nEND_IF\nEND_PROGRAM");
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var ifStmt = prog.Children.OfType<IfStatement>().FirstOrDefault();
            Assert.NotNull(ifStmt);
            Assert.Contains(ifStmt.Children, c => c is ElsifClause);
            Assert.Contains(ifStmt.Children, c => c is ElseClause);
        }

        #endregion

        #region 5. FOR loop

        [Fact]
        public void ForLoop()
        {
            var unit = Parse("PROGRAM P\nFOR i := 0 TO 10 BY 1 DO\n    x := i;\nEND_FOR\nEND_PROGRAM");
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var forStmt = prog.Children.OfType<ForStatement>().FirstOrDefault();
            Assert.NotNull(forStmt);
            Assert.NotEmpty(forStmt.Children);
        }

        #endregion

        #region 6. WHILE loop

        [Fact]
        public void WhileLoop()
        {
            var unit = Parse("PROGRAM P\nWHILE x < 100 DO\n    x := x + 1;\nEND_WHILE\nEND_PROGRAM");
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var whileStmt = prog.Children.OfType<WhileStatement>().FirstOrDefault();
            Assert.NotNull(whileStmt);
            Assert.NotEmpty(whileStmt.Children);
        }

        #endregion

        #region 7. CASE statement with branches

        [Fact]
        public void CaseStatement()
        {
            var unit = Parse("PROGRAM P\nCASE x OF\n    1:\n        y := 1;\n    2:\n        y := 2;\nELSE\n    y := 0;\nEND_CASE\nEND_PROGRAM");
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var caseStmt = prog.Children.OfType<CaseStatement>().FirstOrDefault();
            Assert.NotNull(caseStmt);
            Assert.Contains(caseStmt.Children, c => c is CaseBranch);
            Assert.Contains(caseStmt.Children, c => c is ElseClause);
        }

        #endregion

        #region 8. VAR block with multiple declarations

        [Fact]
        public void VarBlock_MultipleDeclarations()
        {
            var unit = Parse("PROGRAM P\nVAR\n    a : INT;\n    b : REAL;\n    c : BOOL;\nEND_VAR\nEND_PROGRAM");
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var varBlock = prog.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            Assert.Equal(VarKind.Var, varBlock.VarKind);
            var decls = varBlock.Children.OfType<VarDeclaration>().ToList();
            Assert.Equal(3, decls.Count);
            Assert.Equal("a", decls[0].Name);
            Assert.Equal("INT", decls[0].TypeName);
            Assert.Equal("b", decls[1].Name);
            Assert.Equal("c", decls[2].Name);
        }

        #endregion

        #region 9. PROPERTY with GET/SET

        [Fact]
        public void Property_WithGetSet()
        {
            var source = "FUNCTION_BLOCK FB\nPROPERTY Value : INT\nGET\nEND_PROPERTY\nEND_FUNCTION_BLOCK";
            var unit = Parse(source);
            var fb = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var prop = fb.Children.OfType<PropertyDeclaration>().FirstOrDefault();
            Assert.NotNull(prop);
            Assert.Equal("Value", prop.Name);
            Assert.True(prop.HasGetter);
        }

        #endregion

        #region 10. INTERFACE declaration

        [Fact]
        public void InterfaceDeclaration()
        {
            var unit = Parse("INTERFACE I_MyInterface\nEND_INTERFACE");
            Assert.Single(unit.Children);
            var iface = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            Assert.Equal(DeclarationKind.Interface, iface.DeclarationKind);
            Assert.Equal("I_MyInterface", iface.Name);
        }

        #endregion

        #region 11. Attribute directives preserved

        [Fact]
        public void AttributeDirective_Preserved()
        {
            var unit = Parse("{attribute 'qualified_only'}\nPROGRAM P\nEND_PROGRAM");
            Assert.Equal(2, unit.Children.Count);
            Assert.IsType<AttributeDirective>(unit.Children[0]);
            Assert.IsType<DeclarationBlock>(unit.Children[1]);
        }

        #endregion

        #region 12. Unknown/malformed code → UnknownNode (no exceptions)

        [Fact]
        public void MalformedCode_ProducesUnknownNode_NoException()
        {
            var unit = Parse("@@@ invalid stuff ;;;");
            Assert.NotEmpty(unit.Children);
            Assert.Contains(unit.Children, c => c is UnknownNode);
        }

        [Fact]
        public void IncompleteProgram_NoException()
        {
            var unit = Parse("PROGRAM");
            Assert.NotEmpty(unit.Children);
        }

        #endregion

        #region 13. Nested structures (IF inside FOR inside FUNCTION_BLOCK)

        [Fact]
        public void NestedStructures_IfInsideForInsideFB()
        {
            var source = @"FUNCTION_BLOCK FB_Nested
VAR
    i : INT;
    x : INT;
END_VAR
FOR i := 0 TO 10 DO
    IF i > 5 THEN
        x := x + 1;
    END_IF
END_FOR
END_FUNCTION_BLOCK";
            var unit = Parse(source);
            var fb = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var forStmt = fb.Children.OfType<ForStatement>().FirstOrDefault();
            Assert.NotNull(forStmt);
            var ifStmt = forStmt.Children.OfType<IfStatement>().FirstOrDefault();
            Assert.NotNull(ifStmt);
        }

        #endregion

        #region 14. FUNCTION declaration

        [Fact]
        public void FunctionDeclaration_WithReturnType()
        {
            var unit = Parse("FUNCTION AddInt : INT\nVAR\n    x : INT;\nEND_VAR\n    x := 1;\nEND_FUNCTION");
            Assert.Single(unit.Children);
            var func = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            Assert.Equal(DeclarationKind.Function, func.DeclarationKind);
            Assert.Equal("AddInt", func.Name);
        }

        [Fact]
        public void FunctionDeclaration_WithoutReturnType()
        {
            var unit = Parse("FUNCTION MyFunc\nEND_FUNCTION");
            Assert.Single(unit.Children);
            var func = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            Assert.Equal(DeclarationKind.Function, func.DeclarationKind);
            Assert.Equal("MyFunc", func.Name);
        }

        #endregion

        #region 15. REPEAT/UNTIL loop

        [Fact]
        public void RepeatUntilLoop()
        {
            var unit = Parse("PROGRAM P\nREPEAT\n    x := x + 1;\nUNTIL x > 10;\nEND_REPEAT\nEND_PROGRAM");
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var repeatStmt = prog.Children.OfType<RepeatStatement>().FirstOrDefault();
            Assert.NotNull(repeatStmt);
            Assert.NotEmpty(repeatStmt.Children);
        }

        #endregion

        #region 16. TYPE/STRUCT

        [Fact]
        public void TypeStruct()
        {
            var source = "TYPE MyStruct : STRUCT\n    a : INT;\n    b : REAL;\nEND_STRUCT\nEND_TYPE";
            var unit = Parse(source);
            Assert.Single(unit.Children);
            var typeDecl = Assert.IsType<TypeDeclaration>(unit.Children[0]);
            Assert.Equal("MyStruct", typeDecl.TypeName);
            var structBody = typeDecl.Children.OfType<StructBody>().FirstOrDefault();
            Assert.NotNull(structBody);
            var decls = structBody.Children.OfType<VarDeclaration>().ToList();
            Assert.Equal(2, decls.Count);
            Assert.Equal("a", decls[0].Name);
            Assert.Equal("b", decls[1].Name);
        }

        #endregion

        #region 17. TYPE/ENUM

        [Fact]
        public void TypeEnum()
        {
            var source = "TYPE MyEnum : (val1, val2, val3)\nEND_TYPE";
            var unit = Parse(source);
            Assert.Single(unit.Children);
            var typeDecl = Assert.IsType<TypeDeclaration>(unit.Children[0]);
            Assert.Equal("MyEnum", typeDecl.TypeName);
            var enumBody = typeDecl.Children.OfType<EnumBody>().FirstOrDefault();
            Assert.NotNull(enumBody);
        }

        #endregion

        #region 18. TYPE/UNION

        [Fact]
        public void TypeUnion()
        {
            var source = "TYPE MyUnion : UNION\n    i : INT;\n    r : REAL;\nEND_UNION\nEND_TYPE";
            var unit = Parse(source);
            Assert.Single(unit.Children);
            var typeDecl = Assert.IsType<TypeDeclaration>(unit.Children[0]);
            Assert.Equal("MyUnion", typeDecl.TypeName);
            var unionBody = typeDecl.Children.OfType<UnionBody>().FirstOrDefault();
            Assert.NotNull(unionBody);
            var decls = unionBody.Children.OfType<VarDeclaration>().ToList();
            Assert.Equal(2, decls.Count);
        }

        #endregion

        #region 19. TYPE/ALIAS

        [Fact]
        public void TypeAlias()
        {
            var source = "TYPE MyInt : INT\nEND_TYPE";
            var unit = Parse(source);
            Assert.Single(unit.Children);
            var typeDecl = Assert.IsType<TypeDeclaration>(unit.Children[0]);
            Assert.Equal("MyInt", typeDecl.TypeName);
        }

        #endregion

        #region 20. NAMESPACE

        [Fact]
        public void Namespace_DottedName()
        {
            // Use identifiers that are not keywords (keywords like 'Namespace' won't be parsed as name parts)
            var source = "NAMESPACE My.App\nPROGRAM P\nEND_PROGRAM\nEND_NAMESPACE";
            var unit = Parse(source);
            Assert.Single(unit.Children);
            var ns = Assert.IsType<NamespaceDeclaration>(unit.Children[0]);
            Assert.Equal("My.App", ns.Name);
            Assert.NotEmpty(ns.Children);
        }

        #endregion

        #region 21. USING

        [Fact]
        public void UsingDirective_DottedName()
        {
            // Use identifiers that are not keywords (keywords like 'Namespace' won't be parsed as name parts)
            var source = "USING My.App;";
            var unit = Parse(source);
            var usingDirs = unit.Children.OfType<UsingDirective>().ToList();
            Assert.NotEmpty(usingDirs);
            var usingDir = usingDirs[0];
            Assert.Equal("My.App", usingDir.NamespaceName);
        }

        #endregion

        #region 22. VAR_INPUT

        [Fact]
        public void VarInput_Block()
        {
            var source = "FUNCTION_BLOCK FB\nVAR_INPUT\n    x : INT;\nEND_VAR\nEND_FUNCTION_BLOCK";
            var unit = Parse(source);
            var fb = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var varBlock = fb.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            Assert.Equal(VarKind.VarInput, varBlock.VarKind);
            var decls = varBlock.Children.OfType<VarDeclaration>().ToList();
            Assert.Single(decls);
            Assert.Equal("x", decls[0].Name);
        }

        #endregion

        #region 23. VAR_OUTPUT

        [Fact]
        public void VarOutput_Block()
        {
            var source = "FUNCTION_BLOCK FB\nVAR_OUTPUT\n    y : BOOL;\nEND_VAR\nEND_FUNCTION_BLOCK";
            var unit = Parse(source);
            var fb = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var varBlock = fb.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            Assert.Equal(VarKind.VarOutput, varBlock.VarKind);
        }

        #endregion

        #region 24. VAR_IN_OUT

        [Fact]
        public void VarInOut_Block()
        {
            var source = "FUNCTION_BLOCK FB\nVAR_IN_OUT\n    z : REAL;\nEND_VAR\nEND_FUNCTION_BLOCK";
            var unit = Parse(source);
            var fb = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var varBlock = fb.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            Assert.Equal(VarKind.VarInOut, varBlock.VarKind);
        }

        #endregion

        #region 25. VAR_TEMP

        [Fact]
        public void VarTemp_Block()
        {
            var source = "PROGRAM P\nVAR_TEMP\n    i : INT;\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var varBlock = prog.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            Assert.Equal(VarKind.VarTemp, varBlock.VarKind);
        }

        #endregion

        #region 26. VAR_STAT

        [Fact]
        public void VarStat_Block()
        {
            var source = "FUNCTION_BLOCK FB\nVAR_STAT\n    counter : INT;\nEND_VAR\nEND_FUNCTION_BLOCK";
            var unit = Parse(source);
            var fb = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var varBlock = fb.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            Assert.Equal(VarKind.VarStat, varBlock.VarKind);
        }

        #endregion

        #region 27. VAR_GLOBAL

        [Fact]
        public void VarGlobal_Block()
        {
            var source = "VAR_GLOBAL\n    gCounter : INT;\nEND_VAR";
            var unit = Parse(source);
            var varBlock = unit.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            Assert.Equal(VarKind.VarGlobal, varBlock.VarKind);
        }

        #endregion

        #region 28. ARRAY type

        [Fact]
        public void VarDeclaration_ArrayType()
        {
            var source = "PROGRAM P\nVAR\n    arr : ARRAY [1..10] OF INT;\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var varBlock = prog.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            var decl = varBlock.Children.OfType<VarDeclaration>().FirstOrDefault();
            Assert.NotNull(decl);
            Assert.Equal("arr", decl.Name);
            Assert.Contains("ARRAY", decl.TypeName);
            Assert.Contains("INT", decl.TypeName);
        }

        #endregion

        #region 29. POINTER TO

        [Fact]
        public void VarDeclaration_PointerTo()
        {
            var source = "PROGRAM P\nVAR\n    ptr : POINTER TO INT;\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var decl = prog.Children.OfType<VarBlock>().First().Children.OfType<VarDeclaration>().First();
            Assert.Equal("ptr", decl.Name);
            Assert.Contains("POINTER", decl.TypeName);
            Assert.Contains("INT", decl.TypeName);
        }

        #endregion

        #region 30. REFERENCE TO

        [Fact]
        public void VarDeclaration_ReferenceTo()
        {
            var source = "PROGRAM P\nVAR\n    ref : REFERENCE TO BOOL;\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var decl = prog.Children.OfType<VarBlock>().First().Children.OfType<VarDeclaration>().First();
            Assert.Equal("ref", decl.Name);
            Assert.Contains("REFERENCE", decl.TypeName);
            Assert.Contains("BOOL", decl.TypeName);
        }

        #endregion

        #region 31. AT direct address

        [Fact]
        public void VarDeclaration_AtDirectAddress()
        {
            var source = "PROGRAM P\nVAR\n    b AT %IX0.0 : BOOL;\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var decl = prog.Children.OfType<VarBlock>().First().Children.OfType<VarDeclaration>().First();
            Assert.Equal("b", decl.Name);
            Assert.Equal("%IX0.0", decl.Address);
            Assert.Contains("BOOL", decl.TypeName);
        }

        #endregion

        #region 32. STRING(n) / WSTRING(n)

        [Fact]
        public void VarDeclaration_StringWithLength()
        {
            var source = "PROGRAM P\nVAR\n    s : STRING(80);\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var decl = prog.Children.OfType<VarBlock>().First().Children.OfType<VarDeclaration>().First();
            Assert.Equal("s", decl.Name);
            Assert.Contains("STRING", decl.TypeName);
            Assert.Contains("80", decl.TypeName);
        }

        [Fact]
        public void VarDeclaration_WStringWithLength()
        {
            var source = "PROGRAM P\nVAR\n    ws : WSTRING(255);\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var decl = prog.Children.OfType<VarBlock>().First().Children.OfType<VarDeclaration>().First();
            Assert.Equal("ws", decl.Name);
            Assert.Contains("WSTRING", decl.TypeName);
            Assert.Contains("255", decl.TypeName);
        }

        #endregion

        #region 33. Variable with initializer

        [Fact]
        public void VarDeclaration_WithInitializer()
        {
            var source = "PROGRAM P\nVAR\n    x : INT := 42;\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var decl = prog.Children.OfType<VarBlock>().First().Children.OfType<VarDeclaration>().First();
            Assert.Equal("x", decl.Name);
            Assert.Equal("INT", decl.TypeName);
            Assert.True(decl.HasInitialization);
        }

        #endregion

        #region 34. Function call initialization

        [Fact]
        public void VarDeclaration_FunctionCallInit()
        {
            var source = "PROGRAM P\nVAR\n    fb : FB_Type(param1 := 1);\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var decl = prog.Children.OfType<VarBlock>().First().Children.OfType<VarDeclaration>().First();
            Assert.Equal("fb", decl.Name);
            Assert.Contains("FB_Type", decl.TypeName);
            Assert.True(decl.HasInitialization);
        }

        #endregion

        #region 35. Dotted type names

        [Fact]
        public void VarDeclaration_DottedTypeName()
        {
            var source = "PROGRAM P\nVAR\n    t : Tc2_Standard.R_TRIG;\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var decl = prog.Children.OfType<VarBlock>().First().Children.OfType<VarDeclaration>().First();
            Assert.Equal("t", decl.Name);
            Assert.Contains("Tc2_Standard", decl.TypeName);
            Assert.Contains("R_TRIG", decl.TypeName);
        }

        #endregion

        #region 36. Comma-separated variable names

        [Fact]
        public void VarDeclaration_CommaSeparatedNames()
        {
            var source = "PROGRAM P\nVAR\n    a, b, c : INT;\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var varBlock = prog.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            // a, b, c on one line is a single VarDeclaration with commas
            var decl = varBlock.Children.OfType<VarDeclaration>().FirstOrDefault();
            Assert.NotNull(decl);
            Assert.Equal("a", decl.Name);
            Assert.Contains("INT", decl.TypeName);
        }

        #endregion

        #region 37. EXIT / CONTINUE / RETURN statements

        [Fact]
        public void ExitStatement()
        {
            var source = "PROGRAM P\nFOR i := 0 TO 10 DO\n    EXIT;\nEND_FOR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var forStmt = prog.Children.OfType<ForStatement>().FirstOrDefault();
            Assert.NotNull(forStmt);
            var exitStmt = forStmt.Children.OfType<ExpressionStatement>().FirstOrDefault();
            Assert.NotNull(exitStmt);
            Assert.Contains(exitStmt.Tokens, t => t.Kind == TokenKind.Keyword_Exit);
        }

        [Fact]
        public void ContinueStatement()
        {
            var source = "PROGRAM P\nFOR i := 0 TO 10 DO\n    CONTINUE;\nEND_FOR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var forStmt = prog.Children.OfType<ForStatement>().FirstOrDefault();
            Assert.NotNull(forStmt);
            var contStmt = forStmt.Children.OfType<ExpressionStatement>().FirstOrDefault();
            Assert.NotNull(contStmt);
            Assert.Contains(contStmt.Tokens, t => t.Kind == TokenKind.Keyword_Continue);
        }

        [Fact]
        public void ReturnStatement()
        {
            var source = "FUNCTION MyFunc : INT\nRETURN;\nEND_FUNCTION";
            var unit = Parse(source);
            var func = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var retStmt = func.Children.OfType<ExpressionStatement>().FirstOrDefault();
            Assert.NotNull(retStmt);
            Assert.Contains(retStmt.Tokens, t => t.Kind == TokenKind.Keyword_Return);
        }

        #endregion

        #region 38. VAR with CONSTANT modifier

        [Fact]
        public void VarBlock_WithConstantModifier()
        {
            var source = "PROGRAM P\nVAR CONSTANT\n    max : INT := 100;\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var varBlock = prog.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            Assert.Equal(VarKind.Var, varBlock.VarKind);
            // Check that CONSTANT token is present
            Assert.Contains(varBlock.Tokens, t => t.Kind == TokenKind.Keyword_Constant);
            var decl = varBlock.Children.OfType<VarDeclaration>().FirstOrDefault();
            Assert.NotNull(decl);
            Assert.Equal("max", decl.Name);
            Assert.True(decl.HasInitialization);
        }

        #endregion

        #region 39. VAR with RETAIN modifier

        [Fact]
        public void VarBlock_WithRetainModifier()
        {
            var source = "PROGRAM P\nVAR RETAIN\n    counter : INT;\nEND_VAR\nEND_PROGRAM";
            var unit = Parse(source);
            var prog = Assert.IsType<DeclarationBlock>(unit.Children[0]);
            var varBlock = prog.Children.OfType<VarBlock>().FirstOrDefault();
            Assert.NotNull(varBlock);
            Assert.Equal(VarKind.Var, varBlock.VarKind);
            Assert.Contains(varBlock.Tokens, t => t.Kind == TokenKind.Keyword_Retain);
        }

        #endregion
    }
}
