using System.Collections.Generic;
using STFormatterCore.Lexer;
using STFormatterCore.Parser.Nodes;

namespace STFormatterCore.Parser
{
    /// <summary>
    /// Recursive descent parser that produces a Concrete Syntax Tree (CST) from tokens.
    /// Parse depth: structural level is fully parsed; expression level uses bracket matching only.
    /// Error recovery: unrecognized constructs become UnknownNode; parser never throws on malformed input.
    /// </summary>
    public sealed class STParser
    {
        private readonly List<Token> _tokens;
        private int _position;

        public STParser(List<Token> tokens)
        {
            _tokens = tokens ?? new List<Token>();
            _position = 0;
        }

        #region Public API

        public CompilationUnit Parse()
        {
            var unit = new CompilationUnit();

            while (!IsAtEnd)
            {
                var decl = ParseDeclaration();
                if (decl != null)
                    unit.AddChild(decl);
            }

            return unit;
        }

        #endregion

        #region Token Helpers

        private Token Current => _position < _tokens.Count
            ? _tokens[_position]
            : new Token(TokenKind.EndOfFile, "", -1);

        private Token Peek(int offset = 0)
        {
            int idx = _position + offset;
            return idx < _tokens.Count
                ? _tokens[idx]
                : new Token(TokenKind.EndOfFile, "", -1);
        }

        private Token Advance()
        {
            var token = Current;
            if (_position < _tokens.Count)
                _position++;
            return token;
        }

        private Token Expect(TokenKind kind)
        {
            if (Current.Kind == kind)
                return Advance();

            // Error recovery: create a bad token marker
            return new Token(kind, "", Current.Position);
        }

        private bool Match(TokenKind kind)
        {
            if (Current.Kind == kind)
            {
                _position++;
                return true;
            }
            return false;
        }

        private bool IsAtEnd => _position >= _tokens.Count || Current.Kind == TokenKind.EndOfFile;

        private bool MatchKeyword(TokenKind keywordKind) => Match(keywordKind);

        #endregion

        #region Top-Level Parsing

        private SyntaxNode ParseDeclaration()
        {
            switch (Current.Kind)
            {
                case TokenKind.Keyword_Program:
                    return ParseProgram();
                case TokenKind.Keyword_Function:
                    return ParseFunction();
                case TokenKind.Keyword_FunctionBlock:
                    return ParseFunctionBlock();
                case TokenKind.Keyword_Interface:
                    return ParseInterface();
                case TokenKind.Keyword_Type:
                    return ParseTypeDeclaration();
                case TokenKind.Keyword_Namespace:
                    return ParseNamespace();
                case TokenKind.Keyword_Using:
                    return ParseUsing();
                case TokenKind.Pragma:
                    return ParseAttributeDirective();
                case TokenKind.Keyword_VarGlobal:
                    return ParseVarBlock();
                case TokenKind.EndOfFile:
                    return null;
                default:
                    return ParseUnknown();
            }
        }

        #endregion

        #region POU Declarations

        private DeclarationBlock ParseProgram()
        {
            var node = new DeclarationBlock { DeclarationKind = DeclarationKind.Program };
            node.AddToken(Advance()); // PROGRAM

            // Name
            if (Current.Kind == TokenKind.Identifier)
            {
                node.Name = Current.Text;
                node.AddToken(Advance());
            }

            // Optional EXTENDS
            ParseExtendsImplements(node);

            // Body: VAR blocks, statements, END_PROGRAM
            ParseDeclarationBody(node, TokenKind.Keyword_EndProgram);

            if (Current.Kind == TokenKind.Keyword_EndProgram)
                node.AddToken(Advance());

            return node;
        }

        private DeclarationBlock ParseFunction()
        {
            var node = new DeclarationBlock { DeclarationKind = DeclarationKind.Function };
            node.AddToken(Advance()); // FUNCTION

            // Optional return type before name (TwinCAT: FUNCTION name : returnType)
            // Name
            if (Current.Kind == TokenKind.Identifier)
            {
                node.Name = Current.Text;
                node.AddToken(Advance());
            }

            // Optional return type after colon
            if (Current.Kind == TokenKind.BadToken && Current.Text == ":")
            {
                node.AddToken(Advance()); // colon (lexer produces BadToken for lone ':')
                if (Current.Kind == TokenKind.Identifier)
                    node.AddToken(Advance()); // return type name
            }

            // Optional EXTENDS/IMPLEMENTS
            ParseExtendsImplements(node);

            ParseDeclarationBody(node, TokenKind.Keyword_EndFunction);

            if (Current.Kind == TokenKind.Keyword_EndFunction)
                node.AddToken(Advance());

            return node;
        }

        private DeclarationBlock ParseFunctionBlock()
        {
            var node = new DeclarationBlock { DeclarationKind = DeclarationKind.FunctionBlock };
            node.AddToken(Advance()); // FUNCTION_BLOCK

            // Name
            if (Current.Kind == TokenKind.Identifier)
            {
                node.Name = Current.Text;
                node.AddToken(Advance());
            }

            // Optional EXTENDS/IMPLEMENTS
            ParseExtendsImplements(node);

            ParseDeclarationBody(node, TokenKind.Keyword_EndFunctionBlock);

            if (Current.Kind == TokenKind.Keyword_EndFunctionBlock)
                node.AddToken(Advance());

            return node;
        }

        private DeclarationBlock ParseInterface()
        {
            var node = new DeclarationBlock { DeclarationKind = DeclarationKind.Interface };
            node.AddToken(Advance()); // INTERFACE

            // Name
            if (Current.Kind == TokenKind.Identifier)
            {
                node.Name = Current.Text;
                node.AddToken(Advance());
            }

            // Optional EXTENDS
            ParseExtendsImplements(node);

            ParseDeclarationBody(node, TokenKind.Keyword_EndInterface);

            if (Current.Kind == TokenKind.Keyword_EndInterface)
                node.AddToken(Advance());

            return node;
        }

        private void ParseExtendsImplements(DeclarationBlock node)
        {
            if (Current.Kind == TokenKind.Keyword_Extends)
            {
                node.AddToken(Advance()); // EXTENDS
                if (Current.Kind == TokenKind.Identifier)
                {
                    node.ExtendsName = Current.Text;
                    node.AddToken(Advance());
                }
            }

            if (Current.Kind == TokenKind.Keyword_Implements)
            {
                node.AddToken(Advance()); // IMPLEMENTS
                // Parse comma-separated list
                do
                {
                    if (Current.Kind == TokenKind.Identifier)
                    {
                        node.ImplementsNames.Add(Current.Text);
                        node.AddToken(Advance());
                    }
                } while (Match(TokenKind.Comma));
            }
        }

        private void ParseDeclarationBody(DeclarationBlock node, TokenKind endKeyword)
        {
            while (!IsAtEnd && Current.Kind != endKeyword)
            {
                if (IsVarKeyword(Current.Kind))
                {
                    node.AddChild(ParseVarBlock());
                }
                else if (Current.Kind == TokenKind.Keyword_Method || IsAccessModifier(Current.Kind))
                {
                    node.AddChild(ParseMethod());
                }
                else if (Current.Kind == TokenKind.Keyword_Property ||
                         (IsAccessModifier(Current.Kind) && Peek(1).Kind == TokenKind.Keyword_Property))
                {
                    node.AddChild(ParseProperty());
                }
                else if (Current.Kind == TokenKind.Pragma)
                {
                    node.AddChild(ParseAttributeDirective());
                }
                else if (Current.Kind == TokenKind.Keyword_Type)
                {
                    node.AddChild(ParseTypeDeclaration());
                }
                else if (Current.Kind == TokenKind.Keyword_Namespace)
                {
                    node.AddChild(ParseNamespace());
                }
                else if (Current.Kind == TokenKind.Keyword_Using)
                {
                    node.AddChild(ParseUsing());
                }
                else if (Current.Kind == endKeyword)
                {
                    break;
                }
                else
                {
                    // Statement inside POU body
                    node.AddChild(ParseStatement());
                }
            }
        }

        #endregion

        #region VAR Blocks

        private VarBlock ParseVarBlock()
        {
            var node = new VarBlock();
            var kind = Current.Kind;
            node.VarKind = MapVarKind(kind);
            node.AddToken(Advance()); // VAR keyword

            // Optional modifiers: CONSTANT, RETAIN, PERSISTENT
            while (Current.Kind == TokenKind.Keyword_Constant ||
                   Current.Kind == TokenKind.Keyword_Retain ||
                   Current.Kind == TokenKind.Keyword_Persistent)
            {
                node.AddToken(Advance());
            }

            // Parse variable declarations until END_VAR
            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndVar)
            {
                if (Current.Kind == TokenKind.Pragma)
                {
                    node.AddChild(ParseAttributeDirective());
                }
                else if (Current.Kind == TokenKind.Identifier)
                {
                    node.AddChild(ParseVarDeclaration());
                }
                else if (Current.Kind == TokenKind.Semicolon)
                {
                    Advance(); // skip stray semicolons
                }
                else
                {
                    // Recovery: an unexpected token cannot be a declaration start.
                    // Collect the whole construct as an opaque node (emitted verbatim
                    // by the visitor) instead of leaking raw tokens onto the block
                    // where VisitVarBlock would re-flow and corrupt them.
                    node.AddChild(ParseUnknown());
                }
            }

            if (Current.Kind == TokenKind.Keyword_EndVar)
                node.AddToken(Advance()); // END_VAR

            return node;
        }

        private VarDeclaration ParseVarDeclaration()
        {
            var node = new VarDeclaration();

            // Name
            if (Current.Kind == TokenKind.Identifier)
            {
                node.Name = Current.Text;
                node.AddToken(Advance());
            }

            // Comma-separated variable names: bBusy, bError : BOOL;
            while (Current.Kind == TokenKind.Comma)
            {
                node.AddToken(Advance()); // consume comma
                if (Current.Kind == TokenKind.Identifier)
                    node.AddToken(Advance()); // consume next name
            }

            // Optional AT address. TwinCAT writes this as "AT %I*" / "AT %IX0.0",
            // and some exported files contain a doubled "AT AT%I*". Treat every
            // consecutive AT keyword plus the following direct address as one
            // address binding, keeping all tokens so nothing is dropped.
            if (Current.Kind == TokenKind.Keyword_At)
            {
                var addressParts = new System.Text.StringBuilder();
                while (Current.Kind == TokenKind.Keyword_At)
                {
                    var atTok = Advance();
                    node.AddToken(atTok);
                }

                if (Current.Kind == TokenKind.DirectAddress)
                {
                    var addrTok = Advance();
                    node.AddToken(addrTok);
                    addressParts.Append(addrTok.Text);
                }
                else if (Current.Kind == TokenKind.Identifier || Current.Kind == TokenKind.IntegerLiteral)
                {
                    var addrTok = Advance();
                    node.AddToken(addrTok);
                    addressParts.Append(addrTok.Text);
                }

                // Address excludes any AT keyword(s) so existing consumers that
                // expect just "%IX0.0" keep working; the keyword tokens remain in
                // node.Tokens for verbatim/content-preserving output.
                node.Address = addressParts.ToString();
            }

            // Colon (BadToken with text ":" from lexer)
            if (Current.Kind == TokenKind.BadToken && Current.Text == ":")
            {
                node.AddToken(Advance()); // colon
            }

            // Type name - collect type tokens until :=, ;, or END_VAR
            var typeTokens = new List<string>();
            while (!IsAtEnd && Current.Kind != TokenKind.Semicolon &&
                   Current.Kind != TokenKind.Assign &&
                   Current.Kind != TokenKind.Keyword_EndVar)
            {
                // Handle RightBracket specially - don't add twice
                if (Current.Kind == TokenKind.RightBracket)
                {
                    typeTokens.Add(Current.Text);
                    node.AddToken(Advance());
                    // After ], check for OF keyword (ARRAY [1..2] OF INT)
                    if (Current.Kind == TokenKind.Keyword_Of)
                    {
                        typeTokens.Add(Current.Text);
                        node.AddToken(Advance());
                        // Continue collecting the element type
                    }
                    else
                    {
                        break;
                    }
                    continue;
                }

                typeTokens.Add(Current.Text);
                node.AddToken(Advance());

                // For STRING/WSTRING, consume parenthesized length spec like (255)
                if (typeTokens.Count == 1 &&
                    (typeTokens[0].Equals("STRING", System.StringComparison.OrdinalIgnoreCase) ||
                     typeTokens[0].Equals("WSTRING", System.StringComparison.OrdinalIgnoreCase)) &&
                    Current.Kind == TokenKind.LeftParen)
                {
                    int depth = 0;
                    while (!IsAtEnd)
                    {
                        if (Current.Kind == TokenKind.LeftParen) depth++;
                        else if (Current.Kind == TokenKind.RightParen) depth--;
                        typeTokens.Add(Current.Text);
                        node.AddToken(Advance());
                        if (depth == 0) break; // Done when closing paren matched
                    }
                    break;
                }

                // For simple types, one token is enough; but handle ARRAY OF, POINTER TO, etc.
                // Also continue through dotted type names like Tc3_EventLogger.I_TcResultEvent
                if (typeTokens.Count >= 1 &&
                    Current.Kind == TokenKind.Dot &&
                    Peek(1).Kind == TokenKind.Identifier)
                {
                    // Dotted type name - continue collecting
                    continue;
                }

                // Break on '(' when type tokens are already collected.
                // This handles function block initialization: fbName : FBType(param1:=val1, ...);
                // The '(' starts the initialization, not part of the type.
                // STRING/WSTRING(255) is already handled above.
                if (typeTokens.Count >= 1 && Current.Kind == TokenKind.LeftParen)
                {
                    break;
                }

                if (typeTokens.Count == 1 &&
                    Current.Kind != TokenKind.Keyword_Array &&
                    Current.Kind != TokenKind.Keyword_Of &&
                    Current.Kind != TokenKind.Keyword_Pointer &&
                    Current.Kind != TokenKind.Keyword_Reference &&
                    Current.Kind != TokenKind.Keyword_To &&
                    Current.Kind != TokenKind.LeftBracket &&
                    Current.Kind != TokenKind.DotDot)
                {
                    break;
                }
            }
            node.TypeName = string.Join(" ", typeTokens);

            // Optional initialization
            // Handle both := and parenthesized initialization: Type(args)
            if (Current.Kind == TokenKind.Assign)
            {
                node.HasInitialization = true;
                node.AddToken(Advance()); // :=
                // Collect initialization expression until ;
                while (!IsAtEnd && Current.Kind != TokenKind.Semicolon)
                {
                    node.AddToken(Advance());
                }
            }
            else if (Current.Kind == TokenKind.LeftParen)
            {
                // Parenthesized initialization: fbName : FBType(param1:=val1, ...);
                node.HasInitialization = true;
                int depth = 0;
                while (!IsAtEnd)
                {
                    if (Current.Kind == TokenKind.LeftParen) depth++;
                    else if (Current.Kind == TokenKind.RightParen)
                    {
                        if (depth <= 0) break;
                        depth--;
                    }
                    else if (depth == 0 && Current.Kind == TokenKind.Semicolon)
                        break;
                    node.AddToken(Advance());
                }
            }

            // Semicolon
            if (Current.Kind == TokenKind.Semicolon)
                node.AddToken(Advance());

            // Skip any extra consecutive semicolons
            while (Current.Kind == TokenKind.Semicolon)
                Advance();

            return node;
        }

        private static bool IsVarKeyword(TokenKind kind)
        {
            return kind == TokenKind.Keyword_Var ||
                   kind == TokenKind.Keyword_VarInput ||
                   kind == TokenKind.Keyword_VarOutput ||
                   kind == TokenKind.Keyword_VarInOut ||
                   kind == TokenKind.Keyword_VarTemp ||
                   kind == TokenKind.Keyword_VarStat ||
                   kind == TokenKind.Keyword_VarInst ||
                   kind == TokenKind.Keyword_VarConstant ||
                   kind == TokenKind.Keyword_VarGlobal ||
                   kind == TokenKind.Keyword_VarExternal ||
                   kind == TokenKind.Keyword_VarAccess ||
                   kind == TokenKind.Keyword_VarConfig;
        }

        private static VarKind MapVarKind(TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.Keyword_Var: return VarKind.Var;
                case TokenKind.Keyword_VarInput: return VarKind.VarInput;
                case TokenKind.Keyword_VarOutput: return VarKind.VarOutput;
                case TokenKind.Keyword_VarInOut: return VarKind.VarInOut;
                case TokenKind.Keyword_VarTemp: return VarKind.VarTemp;
                case TokenKind.Keyword_VarStat: return VarKind.VarStat;
                case TokenKind.Keyword_VarInst: return VarKind.VarInst;
                case TokenKind.Keyword_VarConstant: return VarKind.VarConstant;
                case TokenKind.Keyword_VarGlobal: return VarKind.VarGlobal;
                case TokenKind.Keyword_VarExternal: return VarKind.VarExternal;
                case TokenKind.Keyword_VarAccess: return VarKind.VarAccess;
                case TokenKind.Keyword_VarConfig: return VarKind.VarConfig;
                default: return VarKind.Var;
            }
        }

        #endregion

        #region OOP - Methods and Properties

        private MethodDeclaration ParseMethod()
        {
            var node = new MethodDeclaration();

            // Optional access modifier
            if (IsAccessModifier(Current.Kind))
            {
                node.AccessModifier = Current.Text;
                node.AddToken(Advance());
            }

            // Optional modifiers: ABSTRACT, FINAL, OVERRIDE
            while (Current.Kind == TokenKind.Keyword_Abstract ||
                   Current.Kind == TokenKind.Keyword_Final ||
                   Current.Kind == TokenKind.Keyword_Override)
            {
                if (Current.Kind == TokenKind.Keyword_Abstract) node.IsAbstract = true;
                if (Current.Kind == TokenKind.Keyword_Final) node.IsFinal = true;
                if (Current.Kind == TokenKind.Keyword_Override) node.IsOverride = true;
                node.AddToken(Advance());
            }

            // METHOD keyword
            if (Current.Kind == TokenKind.Keyword_Method)
                node.AddToken(Advance());

            // Name
            if (Current.Kind == TokenKind.Identifier)
            {
                node.Name = Current.Text;
                node.AddToken(Advance());
            }

            // Optional return type after colon
            if (Current.Kind == TokenKind.BadToken && Current.Text == ":")
            {
                node.AddToken(Advance()); // colon
                if (Current.Kind == TokenKind.Identifier)
                {
                    node.ReturnType = Current.Text;
                    node.AddToken(Advance());
                }
            }

            // Body: VAR blocks, statements, END_METHOD
            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndMethod)
            {
                if (IsVarKeyword(Current.Kind))
                {
                    node.AddChild(ParseVarBlock());
                }
                else if (Current.Kind == TokenKind.Pragma)
                {
                    node.AddChild(ParseAttributeDirective());
                }
                else
                {
                    node.AddChild(ParseStatement());
                }
            }

            if (Current.Kind == TokenKind.Keyword_EndMethod)
                node.AddToken(Advance());

            return node;
        }

        private PropertyDeclaration ParseProperty()
        {
            var node = new PropertyDeclaration();

            // Optional access modifier
            if (IsAccessModifier(Current.Kind))
            {
                node.AccessModifier = Current.Text;
                node.AddToken(Advance());
            }

            // PROPERTY keyword
            if (Current.Kind == TokenKind.Keyword_Property)
                node.AddToken(Advance());

            // Name
            if (Current.Kind == TokenKind.Identifier)
            {
                node.Name = Current.Text;
                node.AddToken(Advance());
            }

            // Optional return type after colon
            if (Current.Kind == TokenKind.BadToken && Current.Text == ":")
            {
                node.AddToken(Advance()); // colon
                if (Current.Kind == TokenKind.Identifier)
                {
                    node.ReturnType = Current.Text;
                    node.AddToken(Advance());
                }
            }

            // GET/SET blocks
            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndProperty)
            {
                if (Current.Kind == TokenKind.Pragma)
                {
                    node.AddChild(ParseAttributeDirective());
                }
                else if (Current.Kind == TokenKind.Keyword_Get || IsGetterKeyword(Current.Kind, Current.Text))
                {
                    node.HasGetter = true;
                    node.AddChild(ParseGetterBlock());
                }
                else if (Current.Kind == TokenKind.Keyword_Set || IsSetterKeyword(Current.Kind, Current.Text))
                {
                    node.HasSetter = true;
                    node.AddChild(ParseSetterBlock());
                }
                else
                {
                    // Skip unexpected token
                    node.AddToken(Advance());
                }
            }

            if (Current.Kind == TokenKind.Keyword_EndProperty)
                node.AddToken(Advance());

            return node;
        }

        private GetterBlock ParseGetterBlock()
        {
            var block = new GetterBlock();
            block.AddToken(Advance()); // GET keyword

            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndProperty &&
                   !(Current.Kind == TokenKind.Identifier && Current.Text.Equals("END_GET", System.StringComparison.OrdinalIgnoreCase)))
            {
                if (IsVarKeyword(Current.Kind))
                    block.AddChild(ParseVarBlock());
                else
                    block.AddChild(ParseStatement());
            }

            if (Current.Kind == TokenKind.Identifier &&
                Current.Text.Equals("END_GET", System.StringComparison.OrdinalIgnoreCase))
                block.AddToken(Advance()); // END_GET

            return block;
        }

        private SetterBlock ParseSetterBlock()
        {
            var block = new SetterBlock();
            block.AddToken(Advance()); // SET keyword

            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndProperty &&
                   !(Current.Kind == TokenKind.Identifier && Current.Text.Equals("END_SET", System.StringComparison.OrdinalIgnoreCase)))
            {
                if (IsVarKeyword(Current.Kind))
                    block.AddChild(ParseVarBlock());
                else
                    block.AddChild(ParseStatement());
            }

            if (Current.Kind == TokenKind.Identifier &&
                Current.Text.Equals("END_SET", System.StringComparison.OrdinalIgnoreCase))
                block.AddToken(Advance()); // END_SET

            return block;
        }

        private static bool IsAccessModifier(TokenKind kind)
        {
            return kind == TokenKind.Keyword_Public ||
                   kind == TokenKind.Keyword_Private ||
                   kind == TokenKind.Keyword_Protected ||
                   kind == TokenKind.Keyword_Internal;
        }

        // GET/SET are keywords in the lexer; also check identifier text for backwards compatibility
        private static bool IsGetterKeyword(TokenKind kind, string text = null)
        {
            if (kind == TokenKind.Keyword_Get) return true;
            if (kind == TokenKind.Identifier && text != null)
                return text.Equals("GET", System.StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private static bool IsSetterKeyword(TokenKind kind, string text = null)
        {
            if (kind == TokenKind.Keyword_Set) return true;
            if (kind == TokenKind.Identifier && text != null)
                return text.Equals("SET", System.StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private bool IsCurrentGetter()
        {
            return Current.Kind == TokenKind.Keyword_Get ||
                   (Current.Kind == TokenKind.Identifier &&
                    Current.Text.Equals("GET", System.StringComparison.OrdinalIgnoreCase));
        }

        private bool IsCurrentSetter()
        {
            return Current.Kind == TokenKind.Keyword_Set ||
                   (Current.Kind == TokenKind.Identifier &&
                    Current.Text.Equals("SET", System.StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsEndGetter(TokenKind kind)
        {
            // END_GET is not a keyword in the lexer; it's parsed as identifier "END_GET"
            // We handle it via text matching
            return false; // Will be handled in the loop condition
        }

        private static bool IsEndSetter(TokenKind kind)
        {
            return false;
        }

        #endregion

        #region Statements

        private SyntaxNode ParseStatement()
        {
            switch (Current.Kind)
            {
                case TokenKind.Keyword_If:
                    return ParseIfStatement();
                case TokenKind.Keyword_Case:
                    return ParseCaseStatement();
                case TokenKind.Keyword_For:
                    return ParseForStatement();
                case TokenKind.Keyword_While:
                    return ParseWhileStatement();
                case TokenKind.Keyword_Repeat:
                    return ParseRepeatStatement();
                case TokenKind.Keyword_Exit:
                case TokenKind.Keyword_Continue:
                case TokenKind.Keyword_Return:
                    return ParseSimpleStatement();
                case TokenKind.Identifier:
                    return ParseAssignmentOrCall();
                case TokenKind.Pragma:
                    return ParseAttributeDirective();
                default:
                    return ParseUnknown();
            }
        }

        private IfStatement ParseIfStatement()
        {
            var node = new IfStatement();
            node.AddToken(Advance()); // IF

            // Condition: collect tokens until THEN at bracket depth 0
            CollectExpressionTokens(node, TokenKind.Keyword_Then);

            if (Current.Kind == TokenKind.Keyword_Then)
                node.AddToken(Advance()); // THEN

            // Body statements
            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_Elsif &&
                   Current.Kind != TokenKind.Keyword_Else &&
                   Current.Kind != TokenKind.Keyword_EndIf)
            {
                node.AddChild(ParseStatement());
            }

            // ELSIF clauses
            while (Current.Kind == TokenKind.Keyword_Elsif)
            {
                var elsif = new ElsifClause();
                elsif.AddToken(Advance()); // ELSIF

                CollectExpressionTokens(elsif, TokenKind.Keyword_Then);

                if (Current.Kind == TokenKind.Keyword_Then)
                    elsif.AddToken(Advance()); // THEN

                while (!IsAtEnd && Current.Kind != TokenKind.Keyword_Elsif &&
                       Current.Kind != TokenKind.Keyword_Else &&
                       Current.Kind != TokenKind.Keyword_EndIf)
                {
                    elsif.AddChild(ParseStatement());
                }

                node.AddChild(elsif);
            }

            // ELSE clause
            if (Current.Kind == TokenKind.Keyword_Else)
            {
                var elseClause = new ElseClause();
                elseClause.AddToken(Advance()); // ELSE

                while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndIf)
                {
                    elseClause.AddChild(ParseStatement());
                }

                node.AddChild(elseClause);
            }

            if (Current.Kind == TokenKind.Keyword_EndIf)
                node.AddToken(Advance()); // END_IF

            return node;
        }

        private CaseStatement ParseCaseStatement()
        {
            var node = new CaseStatement();
            node.AddToken(Advance()); // CASE

            // Selector expression until OF
            CollectExpressionTokens(node, TokenKind.Keyword_Of);

            if (Current.Kind == TokenKind.Keyword_Of)
                node.AddToken(Advance()); // OF

            // Case branches
            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndCase &&
                   Current.Kind != TokenKind.Keyword_Else)
            {
                // Skip whitespace-artifact BadTokens between branches,
                // but transfer their leading trivia (comments!) to the next
                // real token so they are not lost.
                while (Current.Kind == TokenKind.BadToken && IsWhitespaceBadToken(Current))
                {
                    TransferTriviaToNext(Current);
                    Advance();
                }

                if (IsAtEnd || Current.Kind == TokenKind.Keyword_EndCase ||
                    Current.Kind == TokenKind.Keyword_Else)
                    break;

                var branch = new CaseBranch();

                // Case values until colon (BadToken with text ":")
                while (!IsAtEnd && !(Current.Kind == TokenKind.BadToken && Current.Text == ":") &&
                       Current.Kind != TokenKind.Keyword_EndCase &&
                       Current.Kind != TokenKind.Keyword_Else)
                {
                    branch.AddToken(Advance());
                }

                // Colon
                if (Current.Kind == TokenKind.BadToken && Current.Text == ":")
                    branch.AddToken(Advance());

                // Body statements until next case value or ELSE or END_CASE
                while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndCase &&
                       Current.Kind != TokenKind.Keyword_Else &&
                       !IsCaseLabelStart())
                {
                    // Skip whitespace-artifact BadTokens (but transfer their trivia)
                    if (Current.Kind == TokenKind.BadToken && IsWhitespaceBadToken(Current))
                    {
                        TransferTriviaToNext(Current);
                        Advance();
                        continue;
                    }
                    // Skip stray colons
                    if (Current.Kind == TokenKind.BadToken && Current.Text == ":")
                    {
                        Advance();
                        continue;
                    }
                    branch.AddChild(ParseStatement());
                }

                node.AddChild(branch);
            }

            // ELSE clause
            if (Current.Kind == TokenKind.Keyword_Else)
            {
                var elseClause = new ElseClause();
                elseClause.AddToken(Advance()); // ELSE

                while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndCase)
                {
                    elseClause.AddChild(ParseStatement());
                }

                node.AddChild(elseClause);
            }

            if (Current.Kind == TokenKind.Keyword_EndCase)
                node.AddToken(Advance()); // END_CASE

            return node;
        }

        private ForStatement ParseForStatement()
        {
            var node = new ForStatement();
            node.AddToken(Advance()); // FOR

            // Counter variable, :=, start, TO, end, optional BY step, DO
            // Collect all tokens until DO at bracket depth 0
            CollectExpressionTokens(node, TokenKind.Keyword_Do);

            if (Current.Kind == TokenKind.Keyword_Do)
                node.AddToken(Advance()); // DO

            // Body
            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndFor)
            {
                node.AddChild(ParseStatement());
            }

            if (Current.Kind == TokenKind.Keyword_EndFor)
                node.AddToken(Advance()); // END_FOR

            return node;
        }

        private WhileStatement ParseWhileStatement()
        {
            var node = new WhileStatement();
            node.AddToken(Advance()); // WHILE

            // Condition until DO
            CollectExpressionTokens(node, TokenKind.Keyword_Do);

            if (Current.Kind == TokenKind.Keyword_Do)
                node.AddToken(Advance()); // DO

            // Body
            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndWhile)
            {
                node.AddChild(ParseStatement());
            }

            if (Current.Kind == TokenKind.Keyword_EndWhile)
                node.AddToken(Advance()); // END_WHILE

            return node;
        }

        private RepeatStatement ParseRepeatStatement()
        {
            var node = new RepeatStatement();
            node.AddToken(Advance()); // REPEAT

            // Body
            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_Until)
            {
                node.AddChild(ParseStatement());
            }

            if (Current.Kind == TokenKind.Keyword_Until)
                node.AddToken(Advance()); // UNTIL

            // Condition until semicolon
            CollectExpressionTokens(node, TokenKind.Semicolon);

            if (Current.Kind == TokenKind.Semicolon)
                node.AddToken(Advance());

            return node;
        }

        private SyntaxNode ParseSimpleStatement()
        {
            var node = new ExpressionStatement();
            node.AddToken(Advance()); // EXIT/CONTINUE/RETURN keyword

            // Optional expression until semicolon
            while (!IsAtEnd && Current.Kind != TokenKind.Semicolon)
            {
                node.AddToken(Advance());
            }

            if (Current.Kind == TokenKind.Semicolon)
                node.AddToken(Advance());

            return node;
        }

        private SyntaxNode ParseAssignmentOrCall()
        {
            // Could be: x := expr; or FB.method(args); or x.member := expr;
            // Collect all tokens until semicolon, tracking bracket depth
            var tokens = new List<Token>();
            bool hasAssign = false;

            while (!IsAtEnd && Current.Kind != TokenKind.Semicolon)
            {
                if (Current.Kind == TokenKind.Assign)
                    hasAssign = true;

                tokens.Add(Advance());
            }

            if (Current.Kind == TokenKind.Semicolon)
                tokens.Add(Advance());

            if (hasAssign)
            {
                var node = new AssignmentStatement();
                foreach (var t in tokens)
                    node.AddToken(t);
                return node;
            }
            else
            {
                var node = new ExpressionStatement();
                foreach (var t in tokens)
                    node.AddToken(t);
                return node;
            }
        }

        /// <summary>
        /// Checks if current position looks like a case label start (value followed eventually by colon).
        /// Skips BadTokens that contain only whitespace/newline characters.
        /// Heuristic: identifier, literal, or keyword that isn't ELSE/END_CASE at bracket depth 0.
        /// </summary>
        private bool IsCaseLabelStart()
        {
            if (Current.Kind == TokenKind.Keyword_Else || Current.Kind == TokenKind.Keyword_EndCase)
                return false;

            // If we see a colon (BadToken with ":") immediately, it's not a label start
            if (Current.Kind == TokenKind.BadToken && Current.Text == ":")
                return false;

            // Skip BadTokens that are actually whitespace/newline artifacts
            // (e.g., \n or \r\n that leaked through the lexer)
            int checkPos = _position;
            while (checkPos < _tokens.Count &&
                   _tokens[checkPos].Kind == TokenKind.BadToken &&
                   IsWhitespaceBadToken(_tokens[checkPos]))
            {
                checkPos++;
            }

            if (checkPos >= _tokens.Count)
                return false;

            var checkToken = _tokens[checkPos];

            // If current (non-trivia) token is identifier, literal, or similar, it could be a case label
            if (checkToken.Kind == TokenKind.Identifier ||
                checkToken.Kind == TokenKind.IntegerLiteral ||
                checkToken.Kind == TokenKind.StringLiteral)
            {
                // Look ahead for a colon before next statement-like keyword
                int saved = checkPos;
                int depth = 0;
                while (saved < _tokens.Count)
                {
                    var k = _tokens[saved].Kind;
                    if (k == TokenKind.LeftParen || k == TokenKind.LeftBracket || k == TokenKind.LeftBrace)
                        depth++;
                    else if (k == TokenKind.RightParen || k == TokenKind.RightBracket || k == TokenKind.RightBrace)
                        depth--;
                    else if (depth == 0 && k == TokenKind.BadToken && _tokens[saved].Text == ":")
                        return true;
                    else if (depth == 0 && (k == TokenKind.Semicolon || k == TokenKind.Keyword_Else ||
                              k == TokenKind.Keyword_EndCase))
                        return false;
                    // Stop at statement keywords that indicate this is not a case label
                    else if (depth == 0 && (k == TokenKind.Keyword_Then || k == TokenKind.Keyword_Do ||
                              k == TokenKind.Keyword_Of || k == TokenKind.Keyword_Until ||
                              IsEndKeyword(k)))
                        return false;
                    saved++;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a BadToken is actually a whitespace/newline artifact.
        /// </summary>
        private static bool IsWhitespaceBadToken(Token token)
        {
            if (token.Kind != TokenKind.BadToken || string.IsNullOrEmpty(token.Text))
                return false;
            foreach (char c in token.Text)
            {
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Transfers leading trivia from a token that will be skipped to the next token,
        /// so that comments are not lost when whitespace-artifact BadTokens are discarded.
        /// </summary>
        private void TransferTriviaToNext(Token skippedToken)
        {
            if (skippedToken.LeadingTrivia == null || skippedToken.LeadingTrivia.Count == 0)
                return;
            int nextIdx = _position + 1;
            if (nextIdx >= _tokens.Count) return;
            var nextTok = _tokens[nextIdx];
            // Insert at beginning so comments appear before the next token's own trivia
            nextTok.LeadingTrivia.InsertRange(0, skippedToken.LeadingTrivia);
        }

        #endregion

        #region Type Declarations

        private TypeDeclaration ParseTypeDeclaration()
        {
            var node = new TypeDeclaration();
            node.AddToken(Advance()); // TYPE

            // Type name
            if (Current.Kind == TokenKind.Identifier)
            {
                node.TypeName = Current.Text;
                node.AddToken(Advance());
            }

            // Colon
            if (Current.Kind == TokenKind.BadToken && Current.Text == ":")
                node.AddToken(Advance());

            // Body: STRUCT, ENUM (parenthesized list), or UNION
            if (Current.Kind == TokenKind.Keyword_Struct)
            {
                node.AddChild(ParseStructBody());
            }
            else if (Current.Kind == TokenKind.Keyword_Union)
            {
                node.AddChild(ParseUnionBody());
            }
            else if (Current.Kind == TokenKind.LeftParen)
            {
                // EnumBody now consumes "( ... ) [":" base] ";" itself, so the whole
                // enum declaration is one child.
                node.AddChild(ParseEnumBody());
            }
            else
            {
                // Unknown type body - collect until END_TYPE
                while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndType)
                    node.AddToken(Advance());
            }

            if (Current.Kind == TokenKind.Keyword_EndType)
                node.AddToken(Advance()); // END_TYPE

            return node;
        }

        private StructBody ParseStructBody()
        {
            var body = new StructBody();
            body.AddToken(Advance()); // STRUCT

            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndStruct)
            {
                if (Current.Kind == TokenKind.Identifier)
                    body.AddChild(ParseVarDeclaration());
                else if (Current.Kind == TokenKind.Pragma)
                    body.AddChild(ParseAttributeDirective());
                else
                    body.AddToken(Advance());
            }

            if (Current.Kind == TokenKind.Keyword_EndStruct)
                body.AddToken(Advance()); // END_STRUCT

            return body;
        }

        private EnumBody ParseEnumBody()
        {
            var body = new EnumBody();
            body.AddToken(Advance()); // (

            // Collect enum values until )
            while (!IsAtEnd && Current.Kind != TokenKind.RightParen)
            {
                body.AddToken(Advance());
            }

            if (Current.Kind == TokenKind.RightParen)
                body.AddToken(Advance()); // )

            // Optional base type then terminating ';' belong to the enum body so the
            // formatter can preserve "TYPE x : (a, b) : INT;" as one unit:
            //   ( ... ) [":" INT] ";"
            if (Current.Kind == TokenKind.BadToken && Current.Text == ":")
                body.AddToken(Advance());
            while (Current.Kind == TokenKind.Identifier ||
                   Current.Kind == TokenKind.IntegerLiteral)
            {
                body.AddToken(Advance());
            }
            if (Current.Kind == TokenKind.Semicolon)
                body.AddToken(Advance());

            return body;
        }

        private UnionBody ParseUnionBody()
        {
            var body = new UnionBody();
            body.AddToken(Advance()); // UNION

            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndUnion)
            {
                if (Current.Kind == TokenKind.Identifier)
                    body.AddChild(ParseVarDeclaration());
                else if (Current.Kind == TokenKind.Pragma)
                    body.AddChild(ParseAttributeDirective());
                else
                    body.AddToken(Advance());
            }

            if (Current.Kind == TokenKind.Keyword_EndUnion)
                body.AddToken(Advance()); // END_UNION

            return body;
        }

        #endregion

        #region Namespace and Using

        private NamespaceDeclaration ParseNamespace()
        {
            var node = new NamespaceDeclaration();
            node.AddToken(Advance()); // NAMESPACE

            // Namespace name (possibly dotted: A.B.C)
            var nameParts = new List<string>();
            if (Current.Kind == TokenKind.Identifier)
            {
                nameParts.Add(Current.Text);
                node.AddToken(Advance());

                while (Current.Kind == TokenKind.Dot)
                {
                    nameParts.Add(".");
                    node.AddToken(Advance());
                    if (Current.Kind == TokenKind.Identifier)
                    {
                        nameParts.Add(Current.Text);
                        node.AddToken(Advance());
                    }
                }
            }
            node.Name = string.Join("", nameParts);

            // Body
            while (!IsAtEnd && Current.Kind != TokenKind.Keyword_EndNamespace)
            {
                var decl = ParseDeclaration();
                if (decl != null)
                    node.AddChild(decl);
            }

            if (Current.Kind == TokenKind.Keyword_EndNamespace)
                node.AddToken(Advance()); // END_NAMESPACE

            return node;
        }

        private UsingDirective ParseUsing()
        {
            var node = new UsingDirective();
            node.AddToken(Advance()); // USING

            // Namespace name (possibly dotted)
            var nameParts = new List<string>();
            if (Current.Kind == TokenKind.Identifier)
            {
                nameParts.Add(Current.Text);
                node.AddToken(Advance());

                while (Current.Kind == TokenKind.Dot)
                {
                    nameParts.Add(".");
                    node.AddToken(Advance());
                    if (Current.Kind == TokenKind.Identifier)
                    {
                        nameParts.Add(Current.Text);
                        node.AddToken(Advance());
                    }
                }
            }
            node.NamespaceName = string.Join("", nameParts);

            // Semicolon
            if (Current.Kind == TokenKind.Semicolon)
                node.AddToken(Advance());

            return node;
        }

        #endregion

        #region Attributes

        private AttributeDirective ParseAttributeDirective()
        {
            var node = new AttributeDirective();
            node.AddToken(Advance()); // Pragma token
            return node;
        }

        #endregion

        #region Expression Helpers (Bracket Matching Only)

        /// <summary>
        /// Collects tokens into node until a terminator is found at bracket depth 0.
        /// Tracks (), [], {} nesting.
        /// </summary>
        private void CollectExpressionTokens(SyntaxNode node, params TokenKind[] terminators)
        {
            int depth = 0;

            while (!IsAtEnd)
            {
                // Check terminators at depth 0
                if (depth == 0)
                {
                    foreach (var term in terminators)
                    {
                        if (Current.Kind == term)
                            return;

                        // Also check for colon (BadToken ":") as terminator
                        if (term == TokenKind.BadToken && Current.Kind == TokenKind.BadToken && Current.Text == ":")
                            return;
                    }
                }

                // Track bracket depth
                if (Current.Kind == TokenKind.LeftParen ||
                    Current.Kind == TokenKind.LeftBracket ||
                    Current.Kind == TokenKind.LeftBrace)
                {
                    depth++;
                }
                else if (Current.Kind == TokenKind.RightParen ||
                         Current.Kind == TokenKind.RightBracket ||
                         Current.Kind == TokenKind.RightBrace)
                {
                    if (depth > 0) depth--;
                }

                node.AddToken(Advance());
            }
        }

        /// <summary>
        /// Parses a balanced expression, matching (), [], {} nesting.
        /// Returns tokens until the matching closer or a terminator.
        /// </summary>
        private List<Token> ParseBalancedExpression()
        {
            var tokens = new List<Token>();
            int depth = 0;

            while (!IsAtEnd)
            {
                if (Current.Kind == TokenKind.LeftParen ||
                    Current.Kind == TokenKind.LeftBracket ||
                    Current.Kind == TokenKind.LeftBrace)
                {
                    depth++;
                }
                else if (Current.Kind == TokenKind.RightParen ||
                         Current.Kind == TokenKind.RightBracket ||
                         Current.Kind == TokenKind.RightBrace)
                {
                    if (depth <= 0) break;
                    depth--;
                }
                else if (depth == 0 && (Current.Kind == TokenKind.Semicolon ||
                         Current.Kind == TokenKind.Keyword_Then ||
                         Current.Kind == TokenKind.Keyword_Do ||
                         Current.Kind == TokenKind.Keyword_Of ||
                         Current.Kind == TokenKind.Keyword_Until ||
                         Current.Kind == TokenKind.Keyword_By ||
                         Current.Kind == TokenKind.Keyword_To))
                {
                    break;
                }

                tokens.Add(Advance());
            }

            return tokens;
        }

        #endregion

        #region Error Recovery

        /// <summary>
        /// Creates an UnknownNode collecting tokens until the next recovery point
        /// (semicolon or END_* keyword at current nesting level).
        /// </summary>
        private UnknownNode ParseUnknown()
        {
            var node = new UnknownNode();

            // Collect at least one token to ensure progress
            if (!IsAtEnd)
                node.AddToken(Advance());

            // If the first token consumed is already a recovery point (semicolon),
            // stop immediately - don't continue consuming past it.
            if (node.Tokens.Count > 0 && node.Tokens[0].Kind == TokenKind.Semicolon)
                return node;

            // Continue until recovery point
            int depth = 0;
            while (!IsAtEnd)
            {
                // Track nesting
                if (Current.Kind == TokenKind.LeftParen ||
                    Current.Kind == TokenKind.LeftBracket ||
                    Current.Kind == TokenKind.LeftBrace)
                {
                    depth++;
                }
                else if (Current.Kind == TokenKind.RightParen ||
                         Current.Kind == TokenKind.RightBracket ||
                         Current.Kind == TokenKind.RightBrace)
                {
                    if (depth > 0)
                    {
                        depth--;
                        node.AddToken(Advance());
                        continue;
                    }
                    else
                    {
                        break; // Don't consume unmatched closer
                    }
                }

                // Recovery at depth 0
                if (depth == 0)
                {
                    if (Current.Kind == TokenKind.Semicolon)
                    {
                        node.AddToken(Advance()); // consume the semicolon
                        break;
                    }

                    // Colon at depth 0 is a case branch boundary - stop here
                    if (Current.Kind == TokenKind.BadToken && Current.Text == ":")
                    {
                        node.AddToken(Advance()); // consume the colon for progress
                        break;
                    }

                    // END_* keywords are recovery points
                    if (IsEndKeyword(Current.Kind))
                        break;

                    // Statement keywords at depth 0 indicate a new statement - stop
                    if (Current.Kind == TokenKind.Keyword_If ||
                        Current.Kind == TokenKind.Keyword_Case ||
                        Current.Kind == TokenKind.Keyword_For ||
                        Current.Kind == TokenKind.Keyword_While ||
                        Current.Kind == TokenKind.Keyword_Repeat ||
                        Current.Kind == TokenKind.Keyword_Return ||
                        Current.Kind == TokenKind.Keyword_Exit ||
                        Current.Kind == TokenKind.Keyword_Continue)
                        break;
                }

                node.AddToken(Advance());
            }

            return node;
        }

        private static bool IsEndKeyword(TokenKind kind)
        {
            return kind == TokenKind.Keyword_EndProgram ||
                   kind == TokenKind.Keyword_EndFunction ||
                   kind == TokenKind.Keyword_EndFunctionBlock ||
                   kind == TokenKind.Keyword_EndInterface ||
                   kind == TokenKind.Keyword_EndVar ||
                   kind == TokenKind.Keyword_EndMethod ||
                   kind == TokenKind.Keyword_EndProperty ||
                   kind == TokenKind.Keyword_EndIf ||
                   kind == TokenKind.Keyword_EndCase ||
                   kind == TokenKind.Keyword_EndFor ||
                   kind == TokenKind.Keyword_EndWhile ||
                   kind == TokenKind.Keyword_EndRepeat ||
                   kind == TokenKind.Keyword_EndType ||
                   kind == TokenKind.Keyword_EndStruct ||
                   kind == TokenKind.Keyword_EndUnion ||
                   kind == TokenKind.Keyword_EndNamespace ||
                   kind == TokenKind.Keyword_EndAction ||
                   kind == TokenKind.Keyword_EndTransition;
        }

        #endregion
    }
}
