using System;
using System.Collections.Generic;
using System.Linq;
using STFormatterCore.Configuration;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Parser.Nodes;

namespace STFormatterCore.Formatter
{
    /// <summary>
    /// Main formatting engine that walks the CST and produces formatted text.
    /// </summary>
    public sealed class STFormatter
    {
        private readonly FormatterOptions _options;
        private LineBuilder _output;
        private IndentationManager _indent;
        private string _lineEnding;

        // Standard type names for TypeCase application
        private static readonly HashSet<string> StandardTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BOOL", "BYTE", "WORD", "DWORD", "LWORD",
            "SINT", "INT", "DINT", "LINT", "USINT", "UINT", "UDINT", "ULINT",
            "REAL", "LREAL", "STRING", "WSTRING",
            "TIME", "DATE", "TIME_OF_DAY", "TOD", "DATE_AND_TIME", "DT",
            "LTIME", "LDATE", "LDT", "LTOD", "BIT"
        };

        public STFormatter(FormatterOptions options)
        {
            _options = options ?? new FormatterOptions();
        }

        /// <summary>
        /// Formats a CST and returns the formatted text.
        /// </summary>
        public string Format(CompilationUnit cst, string originalSource)
        {
            if (cst == null) return originalSource ?? string.Empty;

            _lineEnding = _options.GetLineEnding(originalSource ?? "");
            _output = new LineBuilder(_lineEnding);
            _indent = new IndentationManager(_options.IndentString);

            VisitCompilationUnit(cst);

            var result = RemoveTrailingWhitespace(_output.Build());

            // Normalize blank lines. When KeepEmptyLines is false, remove all blank
            // lines (TcBlack's removeEmptyLines behaviour). When true, collapse runs
            // of 3+ blank lines into at most one so the formatter is a fixed point
            // and re-running it cannot keep accumulating blank lines.
            result = NormalizeBlankLines(result);

            // (MaxLineLength wrapping happens inside WriteExpressionTokens so it is
            // CST-aware and idempotent — not as a text post-pass here.)

            // Ensure single trailing newline (the built text already ends with the
            // last WriteLine's line break — strip all trailing line endings first
            // so exactly one remains, and no blank line is left at the end).
            result = result.TrimEnd(' ', '\t', '\r', '\n') + _lineEnding;

            return result;
        }

        #region Visit Dispatch

        private void Visit(SyntaxNode node)
        {
            if (node == null) return;

            switch (node)
            {
                case CompilationUnit n: VisitCompilationUnit(n); break;
                case DeclarationBlock n: VisitDeclarationBlock(n); break;
                case VarBlock n: VisitVarBlock(n); break;
                case VarDeclaration n: VisitVarDeclaration(n, -1); break;
                case MethodDeclaration n: VisitMethodDeclaration(n); break;
                case PropertyDeclaration n: VisitPropertyDeclaration(n); break;
                case IfStatement n: VisitIfStatement(n); break;
                case ElsifClause n: VisitElsifClause(n); break;
                case ElseClause n: VisitElseClause(n); break;
                case CaseStatement n: VisitCaseStatement(n); break;
                case CaseBranch n: VisitCaseBranch(n); break;
                case ForStatement n: VisitForStatement(n); break;
                case WhileStatement n: VisitWhileStatement(n); break;
                case RepeatStatement n: VisitRepeatStatement(n); break;
                case AssignmentStatement n: VisitAssignmentStatement(n); break;
                case ExpressionStatement n: VisitExpressionStatement(n); break;
                case AttributeDirective n: VisitAttributeDirective(n); break;
                case TypeDeclaration n: VisitTypeDeclaration(n); break;
                case StructBody n: VisitStructBody(n); break;
                case EnumBody n: VisitEnumBody(n); break;
                case UnionBody n: VisitUnionBody(n); break;
                case NamespaceDeclaration n: VisitNamespaceDeclaration(n); break;
                case UsingDirective n: VisitUsingDirective(n); break;
                case GetterBlock n: VisitGetterBlock(n); break;
                case SetterBlock n: VisitSetterBlock(n); break;
                case UnknownNode n: VisitUnknownNode(n); break;
                default:
                    // Fallback: visit children
                    VisitStatementList(node.Children);
                    break;
            }
        }

        #endregion

        #region Compilation Unit

        private void VisitCompilationUnit(CompilationUnit node)
        {
            // Top-level children of a compilation unit (a full POU/METHOD text, or a
            // bare implementation whose statements are parsed at the root). Same
            // sibling handling as a POU body: statement children are indented one
            // level and get a separating blank line around them (max one between
            // neighbours); declaration-scope children stay at column 0. This makes a
            // bare implementation body format exactly like the CLI path that wraps it
            // in PROGRAM __Temp__ ... END_PROGRAM and strips the wrapper afterwards.
            VisitBodyChildren(node.Children);

            // Handle EOF trivia (trailing comments at end of file)
            // The parser doesn't store EOF, but any trailing trivia is on the last child
        }

        #endregion

        #region Body children (declaration vs statement indentation)

        /// <summary>
        /// Visits one child of a POU/METHOD/PROPERTY body. Statement nodes are
        /// indented one level (they are implementation code); declaration-scope
        /// nodes — VAR blocks, nested METHOD/PROPERTY headers, attribute pragmas,
        /// TYPE/NAMESPACE/USING declarations — are written at the current indent so
        /// they line up with their parent header, exactly as TwinCAT lays them out.
        /// </summary>
        private void VisitBodyChild(SyntaxNode child)
        {
            bool isStatement =
                child is IfStatement ||
                child is CaseStatement ||
                child is ForStatement ||
                child is WhileStatement ||
                child is RepeatStatement ||
                child is AssignmentStatement ||
                child is ExpressionStatement ||
                child is UnknownNode;

            if (isStatement)
            {
                using (_indent.Push())
                    Visit(child);
            }
            else
            {
                Visit(child);
            }
        }

        /// <summary>
        /// Visits the children of a POU/METHOD body with the same sibling separation
        /// as <see cref="VisitStatementList"/> but indenting only statement children
        /// (declaration-scope children stay at the header level).
        /// </summary>
        private void VisitBodyChildren(System.Collections.Generic.IList<SyntaxNode> children)
        {
            bool aroundBlocks = _options.BlankLinesAroundStatementBlocks;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                bool block = IsStatementBlock(child);
                bool hasPrev = i > 0;
                bool hasNext = i < children.Count - 1;
                bool prevBlock = hasPrev && IsStatementBlock(children[i - 1]);

                if (aroundBlocks && hasPrev && (block || prevBlock))
                    _output.WriteBlankLine();

                VisitBodyChild(child);

                if (aroundBlocks && hasNext && block)
                    _output.WriteBlankLine();
            }
        }

        /// <summary>
        /// True for the statement nodes that get a separating blank line around
        /// them (IF/CASE/FOR/WHILE/REPEAT).
        /// </summary>
        private static bool IsStatementBlock(SyntaxNode node)
        {
            return node is IfStatement ||
                   node is CaseStatement ||
                   node is ForStatement ||
                   node is WhileStatement ||
                   node is RepeatStatement;
        }

        /// <summary>
        /// Visits a list of statement children of a *nested* body (IF/CASE/FOR/
        /// WHILE/REPEAT bodies, ELSIF/ELSE clauses, GET/SET bodies). No separating
        /// blank lines are inserted here: blank-line separation only applies to the
        /// outermost statement blocks of the implementation, handled by
        /// <see cref="VisitBodyChildren"/> and <see cref="VisitCompilationUnit"/>.
        /// </summary>
        private void VisitStatementList(System.Collections.Generic.IList<SyntaxNode> children)
        {
            for (int i = 0; i < children.Count; i++)
                Visit(children[i]);
        }

        #endregion

        #region Declaration Blocks (PROGRAM, FUNCTION, FUNCTION_BLOCK, INTERFACE)

        private void VisitDeclarationBlock(DeclarationBlock node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write leading trivia of the first token (e.g., header comments)
            if (node.Tokens.Count > 0)
                WriteLeadingTrivia(node.Tokens[0]);

            // Write keyword
            string keyword;
            switch (node.DeclarationKind)
            {
                case DeclarationKind.Program:
                    keyword = "PROGRAM"; break;
                case DeclarationKind.Function:
                    keyword = "FUNCTION"; break;
                case DeclarationKind.FunctionBlock:
                    keyword = "FUNCTION_BLOCK"; break;
                case DeclarationKind.Interface:
                    keyword = "INTERFACE"; break;
                default:
                    keyword = "PROGRAM"; break;
            }

            _output.WriteKeyword(keyword);

            // Write name and other header tokens (everything except last token which is END_*)
            var headerTokens = node.Tokens;
            int endIndex = headerTokens.Count - 1;
            bool hasEndKeyword = endIndex >= 0 && IsEndKeyword(headerTokens[endIndex].Kind);

            if (hasEndKeyword)
            {
                // Write tokens between keyword and END_*
                for (int i = 1; i < endIndex; i++)
                {
                    var tok = headerTokens[i];
                    WriteLeadingTrivia(tok);
                    _output.Write(" ");
                    WriteTokenFormatted(tok);
                }
            }
            else
            {
                // No END keyword found, write all remaining tokens
                for (int i = 1; i < headerTokens.Count; i++)
                {
                    var tok = headerTokens[i];
                    WriteLeadingTrivia(tok);
                    _output.Write(" ");
                    WriteTokenFormatted(tok);
                }
            }

            _output.WriteLine();

            // Visit body children. Statement bodies are indented one level; the
            // declaration-scope children (VAR blocks and nested METHOD/PROPERTY/...)
            // stay at the same level as the POU header, matching TwinCAT's layout
            // where VAR belongs at column 0 directly under METHOD/PROGRAM.
            VisitBodyChildren(node.Children);

            // Blank lines before END
            for (int i = 0; i < _options.BlankLinesBeforeEnd; i++)
                _output.WriteLine();

            // Write END_* keyword AFTER children. Only when the source actually
            // contained it — TwinCAT TcPOU Declaration sections hold a partial POU
            // header (PROGRAM name ... END_VAR) WITHOUT END_PROGRAM, and the
            // formatter must not synthesize a spurious END_PROGRAM there.
            if (hasEndKeyword)
            {
                var endTok = headerTokens[endIndex];
                WriteLeadingTrivia(endTok);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword(endTok.Text);
            }
            _output.WriteLine();
        }

        #endregion

        #region VAR Blocks

        private void VisitVarBlock(VarBlock node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write VAR keyword
            var varKeyword = node.Tokens[0];
            WriteLeadingTrivia(varKeyword);
            _output.WriteKeyword(varKeyword.Text);

            // Write optional modifiers (CONSTANT, RETAIN, PERSISTENT)
            for (int i = 1; i < node.Tokens.Count; i++)
            {
                var tok = node.Tokens[i];
                if (tok.Kind == TokenKind.Keyword_EndVar)
                    break; // Handle END_VAR later
                WriteLeadingTrivia(tok);
                _output.Write(" ");
                _output.WriteKeyword(tok.Text);
            }

            _output.WriteLine();

            // Calculate alignment if needed
            int maxNameLen = 0;
            if (_options.AlignDeclarations)
            {
                foreach (var child in node.Children)
                {
                    if (child is VarDeclaration vd)
                        maxNameLen = Math.Max(maxNameLen, (vd.Name ?? "").Length);
                }
            }

            // Visit declarations with increased indent
            using (_indent.Push())
            {
                foreach (var child in node.Children)
                {
                    if (child is VarDeclaration vd)
                        VisitVarDeclaration(vd, maxNameLen);
                    else
                        Visit(child);
                }
            }

            // Blank lines before END_VAR
            for (int i = 0; i < _options.BlankLinesBeforeEnd; i++)
                _output.WriteLine();

            // Write END_VAR
            var endVar = node.Tokens.LastOrDefault(t => t.Kind == TokenKind.Keyword_EndVar);
            if (endVar != null)
            {
                WriteLeadingTrivia(endVar);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_VAR");
                WriteTrailingTrivia(endVar);
            }
            else
            {
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_VAR");
            }

            _output.WriteLine();

            // Blank lines after VAR block
            for (int i = 0; i < _options.BlankLinesAfterVar; i++)
                _output.WriteLine();
        }

        private void VisitVarDeclaration(VarDeclaration node, int alignWidth)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Robustness gate: this visit method reconstructs the declaration from a
            // role classification (name / AT / address / colon / type / init / ;).
            // If the token stream contains anything the classifier cannot confidently
            // account for, we MUST NOT guess — any dropped or reordered token corrupts
            // compileable TwinCAT code. Fall back to byte-exact verbatim output, the
            // same policy TcBlack uses for lines it does not understand.
            if (!IsVarDeclarationShapeWellFormed(node))
            {
                WriteTokensVerbatim(node.Tokens);
                _output.WriteLine();
                return;
            }

            // Find tokens by role
            Token nameToken = null;
            Token colonToken = null;
            Token endVarToken = null;
            var typeTokens = new List<Token>();
            var initTokens = new List<Token>();
            var extraNameTokens = new List<Token>(); // comma + extra identifiers (a, b, c : TYPE)
            Token atToken = null;
            Token addressToken = null;

            bool pastColon = false;
            bool pastAssign = false;
            bool awaitingNameAfterComma = false;
            bool parenthesizedInit = false; // True when init is Type(args) not := expr
            Token semicolonToken = null;

            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_EndVar)
                {
                    endVarToken = tok;
                    break;
                }

                // Track semicolons separately - they written explicitly
                if (tok.Kind == TokenKind.Semicolon)
                {
                    if (semicolonToken == null)
                        semicolonToken = tok;
                    continue;
                }

                if (!pastColon && tok.Kind == TokenKind.BadToken && tok.Text == ":")
                {
                    colonToken = tok;
                    pastColon = true;
                    continue;
                }

                if (!pastColon && tok.Kind == TokenKind.Keyword_At)
                {
                    atToken = tok;
                    continue;
                }

                if (atToken != null && addressToken == null && !pastColon)
                {
                    addressToken = tok;
                    continue;
                }

                if (!pastColon && tok.Kind == TokenKind.Assign)
                {
                    pastAssign = true;
                    continue;
                }

                // Detect parenthesized initialization: Type(args) after colon
                if (pastColon && !pastAssign && !parenthesizedInit &&
                    tok.Kind == TokenKind.LeftParen)
                {
                    parenthesizedInit = true;
                }

                if (!pastColon)
                {
                    if (tok.Kind == TokenKind.Identifier)
                    {
                        if (nameToken == null)
                            nameToken = tok;
                        else if (awaitingNameAfterComma)
                            extraNameTokens.Add(tok);
                        // else: first name already set and no comma seen — this token
                        // is unexpected; the shape gate above should have rejected it.
                    }
                    else if (tok.Kind == TokenKind.Comma)
                    {
                        awaitingNameAfterComma = true;
                        extraNameTokens.Add(tok); // keep the comma token for writing
                    }
                    else if (tok.Kind == TokenKind.Keyword_At)
                    {
                        atToken = tok;
                    }
                    else if (atToken != null && addressToken == null)
                    {
                        addressToken = tok;
                    }
                    else if (!pastAssign)
                    {
                        // Unknown pre-colon token — must not be silently dropped.
                        // The shape gate should have caught this case; the safest
                        // behaviour is to add it as a type token so it is at least
                        // emitted (never dropped).
                        typeTokens.Add(tok);
                    }
                }
                else if (pastAssign || parenthesizedInit)
                {
                    initTokens.Add(tok);
                }
                else
                {
                    typeTokens.Add(tok);
                }
            }

            // Write name
            if (nameToken != null)
            {
                WriteLeadingTrivia(nameToken);
                _output.Write(nameToken.Text);
            }

            // Write additional comma-separated names: a, b, c : TYPE
            foreach (var extra in extraNameTokens)
            {
                if (extra.Kind == TokenKind.Comma)
                {
                    _output.Write(",");
                }
                else
                {
                    WriteLeadingTrivia(extra);
                    _output.Write(" ");
                    _output.Write(extra.Text);
                }
            }

            // Write AT address
            if (atToken != null)
            {
                WriteLeadingTrivia(atToken);
                _output.Write(" ");
                _output.WriteKeyword("AT");
                if (addressToken != null)
                {
                    _output.Write(" ");
                    _output.Write(addressToken.Text);
                }
            }

            // Write colon with alignment
            if (colonToken != null)
            {
                if (alignWidth > 0 && nameToken != null)
                {
                    int padding = alignWidth - (nameToken.Text ?? "").Length + 1;
                    if (padding < 1) padding = 1;
                    _output.Write(new string(' ', padding));
                }
                else
                {
                    _output.Write(" ");
                }
                _output.Write(":");
                _output.Write(" ");
            }

            // Write type tokens with proper spacing
            for (int j = 0; j < typeTokens.Count; j++)
            {
                var tok = typeTokens[j];
                WriteLeadingTrivia(tok);
                if (j > 0)
                {
                    var prevTypeTok = typeTokens[j - 1];
                    // No space before/after ( [ ) ] for string length specs like WSTRING(255)
                    // No space around . for dotted type names like Tc3_EventLogger.I_TcResultEvent
                    if (tok.Kind == TokenKind.LeftParen || tok.Kind == TokenKind.LeftBracket ||
                        prevTypeTok.Kind == TokenKind.LeftParen || prevTypeTok.Kind == TokenKind.LeftBracket ||
                        tok.Kind == TokenKind.RightParen || tok.Kind == TokenKind.RightBracket ||
                        tok.Kind == TokenKind.Dot || prevTypeTok.Kind == TokenKind.Dot)
                    {
                        // No space
                    }
                    else
                    {
                        _output.Write(" ");
                    }
                }
                WriteTypeToken(tok);
            }

            // Write initialization
            if (initTokens.Count > 0)
            {
                if (pastAssign)
                {
                    // Standard := initialization
                    if (_options.OperatorSpacing)
                        _output.Write(" ");
                    _output.Write(":=");
                    if (_options.OperatorSpacing)
                        _output.Write(" ");
                }
                // For parenthesized init, no := prefix needed
                WriteExpressionTokens(initTokens);
            }

            // Write semicolon
            _output.Write(";");

            // Write trailing trivia of semicolon token (or last meaningful token)
            var triviaSource = semicolonToken ?? node.Tokens.LastOrDefault(t => t.Kind != TokenKind.Keyword_EndVar && t.Kind != TokenKind.Semicolon);
            if (triviaSource != null)
                WriteTrailingTrivia(triviaSource);

            _output.WriteLine();
        }

        /// <summary>
        /// Determines whether the token stream of a variable declaration matches the
        /// exact shape the role classifier in <see cref="VisitVarDeclaration"/> can
        /// handle without dropping tokens:
        ///
        ///   [name (, /name)*] (AT address)? ':' type (':=' init)? ';'
        ///
        /// Any deviation (missing colon/semicolon, more than one AT keyword, a
        /// DirectAddress that is not the AT address, more than one colon, etc.) means
        /// we cannot safely reconstruct the line and must fall back to verbatim output.
        /// </summary>
        private static bool IsVarDeclarationShapeWellFormed(VarDeclaration node)
        {
            int colonCount = 0;
            int semicolonCount = 0;
            int atCount = 0;
            bool colonSeen = false;

            for (int i = 0; i < node.Tokens.Count; i++)
            {
                var tok = node.Tokens[i];

                switch (tok.Kind)
                {
                    case TokenKind.Keyword_EndVar:
                        // END_VAR must never be part of a declaration owned by the block;
                        // if it leaked in here the parser split was already wrong.
                        return false;

                    case TokenKind.Semicolon:
                        semicolonCount++;
                        colonSeen = true; // semicolon terminates the declaration
                        break;

                    case TokenKind.Keyword_At:
                        atCount++;
                        break;

                    case TokenKind.DirectAddress:
                        // A direct address (%) is only ever safe in two positions:
                        //  - immediately after a single AT keyword (the AT address),
                        //  - inside the initialisation expression (after ':=').
                        // The parser only produces "AT %addr" sequences correctly for
                        // the first case; anything else (e.g. "x AT AT%I* : ..." or a
                        // stray % token) cannot be classified and must be left alone.
                        {
                            bool afterColon = colonSeen;
                            bool followingSingleAt =
                                atCount == 1 &&
                                i - 1 >= 0 &&
                                node.Tokens[i - 1].Kind == TokenKind.Keyword_At;
                            if (!afterColon && !followingSingleAt)
                                return false;
                        }
                        break;

                    case TokenKind.BadToken:
                        if (tok.Text == ":")
                        {
                            colonCount++;
                            colonSeen = true;
                        }
                        break;

                    case TokenKind.Assign:
                        // ':=' starts the initialisation; tokens after it are fully
                        // supported via initTokens.
                        break;

                    default:
                        break;
                }

                // Track that a directly-following AT address was consumed.
                if (tok.Kind == TokenKind.Keyword_At && i + 1 < node.Tokens.Count)
                {
                    var next = node.Tokens[i + 1].Kind;
                    if (next == TokenKind.DirectAddress ||
                        next == TokenKind.Identifier ||
                        next == TokenKind.IntegerLiteral)
                    {
                        // fine — AT followed by a recognisable address token
                    }
                    else
                    {
                        // "AT" with no recognisable address right after it —
                        // the classifier would leave the address token behind.
                        return false;
                    }
                }
            }

            // The role classifier requires exactly one colon and one semicolon.
            if (colonCount != 1) return false;
            if (semicolonCount != 1) return false;

            // Two consecutive AT keywords (e.g. malformed "x AT AT%I* : ...") is the
            // case that triggered content corruption; refuse to guess.
            if (atCount > 1) return false;

            return true;
        }

        /// <summary>
        /// Writes a node's tokens preserving every token's original text and its
        /// comments exactly as they appeared in the source. Line layout follows the
        /// formatter's policies: continuation lines are re-indented at the current
        /// level, and with KeepEmptyLines=false runs of source newlines collapse to
        /// a single line break — but separate source lines are NEVER merged and
        /// intra-line whitespace is NEVER dropped. Merging lines would let a '//'
        /// comment swallow the code that follows it, and dropping spaces glues
        /// tokens together until the code no longer compiles.
        /// </summary>
        private void WriteTokensVerbatim(System.Collections.Generic.IEnumerable<Token> tokens)
        {
            WriteTokensVerbatim(tokens, null);
        }

        /// <summary>
        /// Same as above, but the caller has already emitted one token and hands over
        /// that token's trailing trivia so it is emitted in the correct position.
        /// </summary>
        private void WriteTokensVerbatim(System.Collections.Generic.IEnumerable<Token> tokens,
                                         System.Collections.Generic.List<Trivia> seedTrailingTrivia)
        {
            // The caller has already emitted content when it hands over seed trivia.
            bool emittedContent = seedTrailingTrivia != null;
            int pendingNewlines = 0;

            void Emit(Trivia trivia)
            {
                switch (trivia.Kind)
                {
                    case TriviaKind.NewLine:
                        pendingNewlines++;
                        break;
                    case TriviaKind.Whitespace:
                        // Line-start whitespace is replaced by the indent written when
                        // newlines are flushed; mid-line whitespace must stay or the
                        // tokens on either side of it would be glued together.
                        if (pendingNewlines == 0 && emittedContent)
                            _output.Write(trivia.Text);
                        break;
                    case TriviaKind.SingleLineComment:
                    case TriviaKind.MultiLineComment:
                        FlushNewlines();
                        _output.Write(trivia.Text);
                        emittedContent = true;
                        break;
                }
            }

            void FlushNewlines()
            {
                if (pendingNewlines == 0) return;
                if (emittedContent)
                {
                    int count = _options.KeepEmptyLines ? pendingNewlines : 1;
                    for (int i = 0; i < count; i++)
                        _output.WriteLine();
                    _output.WriteIndent(_indent.CurrentIndent);
                }
                pendingNewlines = 0;
            }

            if (seedTrailingTrivia != null)
                foreach (var trivia in seedTrailingTrivia)
                    Emit(trivia);

            foreach (var tok in tokens)
            {
                if (tok.LeadingTrivia != null)
                    foreach (var trivia in tok.LeadingTrivia)
                        Emit(trivia);

                FlushNewlines();
                _output.Write(tok.Text);
                emittedContent = true;

                if (tok.TrailingTrivia != null)
                    foreach (var trivia in tok.TrailingTrivia)
                        Emit(trivia);
            }

            // Newlines pending after the last token are redundant: every caller
            // terminates the construct with its own WriteLine().
        }

        #endregion

        #region Method Declaration

        private void VisitMethodDeclaration(MethodDeclaration node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write header tokens
            bool wroteFirst = false;
            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_EndMethod)
                    break;

                WriteLeadingTrivia(tok);

                if (wroteFirst)
                    _output.Write(" ");

                // Write keyword or identifier
                if (IsKeyword(tok.Kind))
                    _output.WriteKeyword(tok.Text);
                else if (tok.Kind == TokenKind.BadToken && tok.Text == ":")
                    _output.Write(":");
                else
                    _output.Write(FormatTypeTokenText(tok));

                wroteFirst = true;
            }

            _output.WriteLine();

            // Visit body with the VAR blocks kept at the METHOD header's level
            // (TwinCAT layout: METHOD at column 0, VAR_INPUT / VAR_OUTPUT / VAR
            // directly under it, member declarations indented one level).
            VisitBodyChildren(node.Children);

            // Blank lines before END_METHOD
            for (int i = 0; i < _options.BlankLinesBeforeEnd; i++)
                _output.WriteLine();

            // Write END_METHOD. TwinCAT TcPOU files store the METHOD header + VAR
            // blocks in the Declaration node and the statements in the Implementation
            // node, so the Declaration text frequently has NO END_METHOD. Only write
            // END_METHOD when the source actually contained it — never synthesize one.
            var endTok = node.Tokens.LastOrDefault(t => t.Kind == TokenKind.Keyword_EndMethod);
            if (endTok != null)
            {
                WriteLeadingTrivia(endTok);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_METHOD");
                WriteTrailingTrivia(endTok);
            }
            _output.WriteLine();
        }

        #endregion

        #region Property Declaration

        private void VisitPropertyDeclaration(PropertyDeclaration node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write header tokens
            bool wroteFirst = false;
            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_EndProperty)
                    break;

                WriteLeadingTrivia(tok);

                if (wroteFirst)
                    _output.Write(" ");

                if (IsKeyword(tok.Kind))
                    _output.WriteKeyword(tok.Text);
                else if (tok.Kind == TokenKind.BadToken && tok.Text == ":")
                    _output.Write(":");
                else
                    _output.Write(FormatTypeTokenText(tok));

                wroteFirst = true;
            }

            _output.WriteLine();

            // Visit GET/SET blocks at the PROPERTY header's level (TwinCAT layout:
            // PROPERTY at column 0, GET/SET directly under it, bodies indented).
            VisitBodyChildren(node.Children);

            // Blank lines before END_PROPERTY
            for (int i = 0; i < _options.BlankLinesBeforeEnd; i++)
                _output.WriteLine();

            // Write END_PROPERTY. TwinCAT stores the PROPERTY header + VAR blocks in
            // the Declaration node and the accessors in the Implementation node, so the
            // Declaration text frequently has NO END_PROPERTY. Only write it when the
            // source actually contained it — never synthesize one.
            var endTok = node.Tokens.LastOrDefault(t => t.Kind == TokenKind.Keyword_EndProperty);
            if (endTok != null)
            {
                WriteLeadingTrivia(endTok);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_PROPERTY");
                WriteTrailingTrivia(endTok);
            }
            _output.WriteLine();
        }

        private void VisitGetterBlock(GetterBlock node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write GET keyword
            if (node.Tokens.Count > 0)
            {
                WriteLeadingTrivia(node.Tokens[0]);
                _output.WriteKeyword("GET");
            }
            _output.WriteLine();

            // Visit body
            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }

            // Write END_GET
            var endTok = node.Tokens.LastOrDefault(t =>
                t.Kind == TokenKind.Identifier &&
                t.Text.Equals("END_GET", StringComparison.OrdinalIgnoreCase));
            if (endTok != null)
            {
                WriteLeadingTrivia(endTok);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_GET");
                WriteTrailingTrivia(endTok);
            }
            _output.WriteLine();
        }

        private void VisitSetterBlock(SetterBlock node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write SET keyword
            if (node.Tokens.Count > 0)
            {
                WriteLeadingTrivia(node.Tokens[0]);
                _output.WriteKeyword("SET");
            }
            _output.WriteLine();

            // Visit body
            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }

            // Write END_SET
            var endTok = node.Tokens.LastOrDefault(t =>
                t.Kind == TokenKind.Identifier &&
                t.Text.Equals("END_SET", StringComparison.OrdinalIgnoreCase));
            if (endTok != null)
            {
                WriteLeadingTrivia(endTok);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_SET");
                WriteTrailingTrivia(endTok);
            }
            _output.WriteLine();
        }

        #endregion

        #region Control Flow Statements

        private void VisitIfStatement(IfStatement node)
        {
            string ifIndent = _indent.CurrentIndent;
            _output.WriteIndent(ifIndent);

            // Separate tokens into IF header (IF keyword + condition) and structural keywords
            var condTokens = new List<Token>();
            Token thenToken = null;

            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_Then)
                {
                    thenToken = tok;
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_EndIf)
                    continue; // Handle END_IF directly
                if (tok.Kind == TokenKind.Keyword_If)
                {
                    WriteLeadingTrivia(tok);
                    _output.WriteKeyword("IF");
                    continue;
                }
                condTokens.Add(tok);
            }

            // Write condition
            _output.Write(" ");
            WriteExpressionTokens(condTokens);

            // Write THEN
            if (thenToken != null)
            {
                WriteLeadingTrivia(thenToken);
                _output.Write(" ");
                _output.WriteKeyword("THEN");
            }
            _output.WriteLine();

            // Visit body children with increased indent.
            // ELSIF/ELSE/END_IF are written at the IF indent level (saved above).
            // This is a nested body, so no blank-line separation is inserted here.
            using (_indent.Push())
            {
                foreach (var child in node.Children)
                {
                    if (child is ElsifClause elsif)
                    {
                        // Write ELSIF at IF level (not body level)
                        _output.WriteIndent(ifIndent);
                        WriteElsifAtParentLevel(elsif);
                    }
                    else if (child is ElseClause elseClause)
                    {
                        // Write ELSE at IF level (not body level)
                        _output.WriteIndent(ifIndent);
                        WriteElseAtParentLevel(elseClause);
                    }
                    else
                    {
                        Visit(child);
                    }
                }
            }

            // Write END_IF at IF level
            var endIfToken = node.Tokens.LastOrDefault(t => t.Kind == TokenKind.Keyword_EndIf);
            if (endIfToken != null)
            {
                _output.WriteIndent(ifIndent);
                WriteLeadingTrivia(endIfToken);
                _output.WriteKeyword("END_IF");
                WriteTrailingTrivia(endIfToken);
            }
            else
            {
                _output.WriteIndent(ifIndent);
                _output.WriteKeyword("END_IF");
            }
            _output.WriteLine();

        }

        /// <summary>
        /// Writes a blank line before a clause keyword (ELSIF/ELSE/END_IF) if the original
        /// source had a blank line there, to preserve intentional spacing.
        /// </summary>
        private void WriteClauseBlankLineIfNeeded(Token clauseToken)
        {
            if (clauseToken?.LeadingTrivia == null) return;
            // Only preserve a source blank line before a clause keyword when blank
            // lines are being kept. With KeepEmptyLines=false the blank is dropped
            // (statement-block separators are emitted separately, not here).
            if (!_options.KeepEmptyLines) return;
            int newlineCount = 0;
            foreach (var trivia in clauseToken.LeadingTrivia)
            {
                if (trivia.Kind == TriviaKind.NewLine)
                    newlineCount++;
            }
            // 2+ newlines in leading trivia means there was a blank line in source
            if (newlineCount >= 2)
                _output.WriteBlankLine();
        }

        /// <summary>
        /// Writes an ELSIF clause at the parent (IF) indent level.
        /// The body is visited at the current indent level (already IF+1).
        /// </summary>
        private void WriteElsifAtParentLevel(ElsifClause node)
        {
            var condTokens = new List<Token>();
            Token thenToken = null;

            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_Elsif)
                {
                    WriteLeadingTrivia(tok);
                    _output.WriteKeyword("ELSIF");
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_Then)
                {
                    thenToken = tok;
                    continue;
                }
                condTokens.Add(tok);
            }

            _output.Write(" ");
            WriteExpressionTokens(condTokens);

            if (thenToken != null)
            {
                WriteLeadingTrivia(thenToken);
                _output.Write(" ");
                _output.WriteKeyword("THEN");
            }
            _output.WriteLine();

            // Body is at current indent level (already IF+1 from outer Push)
            VisitStatementList(node.Children);
        }

        /// <summary>
        /// Writes an ELSE clause at the parent (IF) indent level.
        /// The body is visited at the current indent level (already IF+1).
        /// </summary>
        private void WriteElseAtParentLevel(ElseClause node)
        {
            if (node.Tokens.Count > 0)
            {
                WriteLeadingTrivia(node.Tokens[0]);
            }
            _output.WriteKeyword("ELSE");
            _output.WriteLine();

            // Body is at current indent level (already IF+1 from outer Push)
            VisitStatementList(node.Children);
        }

        private void VisitElsifClause(ElsifClause node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            var condTokens = new List<Token>();
            Token thenToken = null;

            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_Elsif)
                {
                    WriteLeadingTrivia(tok);
                    _output.WriteKeyword("ELSIF");
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_Then)
                {
                    thenToken = tok;
                    continue;
                }
                condTokens.Add(tok);
            }

            _output.Write(" ");
            WriteExpressionTokens(condTokens);

            if (thenToken != null)
            {
                WriteLeadingTrivia(thenToken);
                _output.Write(" ");
                _output.WriteKeyword("THEN");
            }
            _output.WriteLine();

            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }
        }

        private void VisitElseClause(ElseClause node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            if (node.Tokens.Count > 0)
            {
                WriteLeadingTrivia(node.Tokens[0]);
                _output.WriteKeyword("ELSE");
            }
            else
            {
                _output.WriteKeyword("ELSE");
            }
            _output.WriteLine();

            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }
        }

        private void VisitCaseStatement(CaseStatement node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            var selectorTokens = new List<Token>();
            Token ofToken = null;
            Token endCaseToken = null;

            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_Case)
                {
                    WriteLeadingTrivia(tok);
                    _output.WriteKeyword("CASE");
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_Of)
                {
                    ofToken = tok;
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_EndCase)
                {
                    endCaseToken = tok;
                    continue;
                }
                selectorTokens.Add(tok);
            }

            _output.Write(" ");
            WriteExpressionTokens(selectorTokens);

            if (ofToken != null)
            {
                WriteLeadingTrivia(ofToken);
                _output.Write(" ");
                _output.WriteKeyword("OF");
            }
            _output.WriteLine();

            // Visit branches with increased indent
            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }

            // Blank lines before END_CASE
            for (int i = 0; i < _options.BlankLinesBeforeEnd; i++)
                _output.WriteLine();

            // Write END_CASE
            if (endCaseToken != null)
            {
                WriteLeadingTrivia(endCaseToken);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_CASE");
                WriteTrailingTrivia(endCaseToken);
            }
            else
            {
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_CASE");
            }
            _output.WriteLine();

        }

        private void VisitCaseBranch(CaseBranch node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write case values (everything except colon).
            // Write comments from leading trivia but skip newlines to prevent
            // comments from splitting the value from its colon.
            bool wroteColon = false;
            Token prevTok = null;
            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.BadToken && tok.Text == ":")
                {
                    _output.Write(":");
                    wroteColon = true;
                    prevTok = tok;
                    continue;
                }
                // Write comments from leading trivia
                if (tok.LeadingTrivia != null)
                {
                    foreach (var trivia in tok.LeadingTrivia)
                    {
                        if (trivia.Kind == TriviaKind.SingleLineComment ||
                            trivia.Kind == TriviaKind.MultiLineComment)
                        {
                            if (_output.IsAtLineStart)
                                _output.WriteIndent(_indent.CurrentIndent);
                            _output.Write(trivia.Text);
                            if (trivia.Kind == TriviaKind.SingleLineComment)
                            {
                                _output.WriteLine();
                                // After comment newline, re-write indent for the label
                                _output.WriteIndent(_indent.CurrentIndent);
                            }
                        }
                        // Skip NewLine/Whitespace trivia
                    }
                }
                // No space before comma or colon
                if (prevTok != null &&
                    prevTok.Kind != TokenKind.Comma &&
                    tok.Kind != TokenKind.Comma &&
                    !(tok.Kind == TokenKind.BadToken && tok.Text == ":"))
                    _output.Write(" ");
                WriteTokenFormatted(tok);
                prevTok = tok;
            }

            if (!wroteColon)
                _output.Write(":");

            _output.WriteLine();

            // Visit body statements
            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }
        }

        private void VisitForStatement(ForStatement node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            var bodyTokens = new List<Token>();
            Token doToken = null;
            Token endForToken = null;

            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_For)
                {
                    WriteLeadingTrivia(tok);
                    _output.WriteKeyword("FOR");
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_Do)
                {
                    doToken = tok;
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_EndFor)
                {
                    endForToken = tok;
                    continue;
                }
                bodyTokens.Add(tok);
            }

            // Write loop control: counter := start TO end BY step
            _output.Write(" ");
            WriteForExpressionTokens(bodyTokens);

            if (doToken != null)
            {
                WriteLeadingTrivia(doToken);
                _output.Write(" ");
                _output.WriteKeyword("DO");
            }
            _output.WriteLine();

            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }

            for (int i = 0; i < _options.BlankLinesBeforeEnd; i++)
                _output.WriteLine();

            if (endForToken != null)
            {
                WriteLeadingTrivia(endForToken);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_FOR");
                WriteTrailingTrivia(endForToken);
            }
            else
            {
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_FOR");
            }
            _output.WriteLine();

        }

        private void VisitWhileStatement(WhileStatement node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            var condTokens = new List<Token>();
            Token doToken = null;
            Token endWhileToken = null;

            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_While)
                {
                    WriteLeadingTrivia(tok);
                    _output.WriteKeyword("WHILE");
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_Do)
                {
                    doToken = tok;
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_EndWhile)
                {
                    endWhileToken = tok;
                    continue;
                }
                condTokens.Add(tok);
            }

            _output.Write(" ");
            WriteExpressionTokens(condTokens);

            if (doToken != null)
            {
                WriteLeadingTrivia(doToken);
                _output.Write(" ");
                _output.WriteKeyword("DO");
            }
            _output.WriteLine();

            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }

            for (int i = 0; i < _options.BlankLinesBeforeEnd; i++)
                _output.WriteLine();

            if (endWhileToken != null)
            {
                WriteLeadingTrivia(endWhileToken);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_WHILE");
                WriteTrailingTrivia(endWhileToken);
            }
            else
            {
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_WHILE");
            }
            _output.WriteLine();

        }

        private void VisitRepeatStatement(RepeatStatement node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            Token untilToken = null;
            var condTokens = new List<Token>();
            Token endRepeatToken = null;

            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_Repeat)
                {
                    WriteLeadingTrivia(tok);
                    _output.WriteKeyword("REPEAT");
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_Until)
                {
                    untilToken = tok;
                    continue;
                }
                if (tok.Kind == TokenKind.Keyword_EndRepeat)
                {
                    endRepeatToken = tok;
                    continue;
                }
                // After UNTIL, tokens are condition; before UNTIL they shouldn't exist
                // but handle gracefully
                if (untilToken != null)
                    condTokens.Add(tok);
            }

            _output.WriteLine();

            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }

            for (int i = 0; i < _options.BlankLinesBeforeEnd; i++)
                _output.WriteLine();

            // Write UNTIL condition
            _output.WriteIndent(_indent.CurrentIndent);
            if (untilToken != null)
            {
                WriteLeadingTrivia(untilToken);
                _output.WriteKeyword("UNTIL");
                _output.Write(" ");
            }
            else
            {
                _output.WriteKeyword("UNTIL");
                _output.Write(" ");
            }
            WriteExpressionTokens(condTokens);
            _output.Write(";");
            _output.WriteLine();

            // Write END_REPEAT
            if (endRepeatToken != null)
            {
                WriteLeadingTrivia(endRepeatToken);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_REPEAT");
                WriteTrailingTrivia(endRepeatToken);
            }
            else
            {
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_REPEAT");
            }
            _output.WriteLine();

        }

        #endregion

        #region Assignment and Expression Statements

        private void VisitAssignmentStatement(AssignmentStatement node)
        {
            _output.WriteIndent(_indent.CurrentIndent);
            WriteStatementTokens(node.Tokens);
        }

        private void VisitExpressionStatement(ExpressionStatement node)
        {
            _output.WriteIndent(_indent.CurrentIndent);
            WriteStatementTokens(node.Tokens);
        }

        /// <summary>
        /// Writes tokens for a statement (assignment or expression) with proper formatting.
        /// </summary>
        private void WriteStatementTokens(List<Token> tokens)
        {
            if (tokens.Count == 0) return;

            // A comment that ends a source line BEFORE the statement is over makes
            // reflow unsafe: collapsing the tokens would move later code behind the
            // comment and silently comment it out. Such statements are preserved
            // token-for-token — the same "don't touch what you can't safely
            // reformat" policy as TcBlack. A comment on the FINAL token is safe:
            // the normal path appends it via WriteTrailingTrivia.
            if (HasMidStatementTrailingComment(tokens))
            {
                // The visit method already started the statement's line; run the
                // first token's leading trivia through the normal policy so source
                // line breaks do not duplicate into blank lines or reset the indent.
                WriteLeadingTrivia(tokens[0]);
                _output.Write(tokens[0].Text);
                WriteTokensVerbatim(tokens.Skip(1), tokens[0].TrailingTrivia);
                _output.WriteLine();
                return;
            }

            // Statement tokens: skip newlines (all on one formatted line)
            WriteExpressionTokens(tokens, preserveMultiLine: false);

            // Write trailing trivia of last token
            var last = tokens[tokens.Count - 1];
            WriteTrailingTrivia(last);
            _output.WriteLine();
        }

        /// <summary>
        /// True when a token that is NOT the last one carries a comment in its
        /// trailing trivia — such a comment ends its source line mid-statement, so
        /// reflowing the statement would pull later code behind the comment.
        /// Comments in LEADING trivia sit on their own line above the statement and
        /// are handled by the normal reflow path (indent + KeepEmptyLines policy).
        /// </summary>
        private static bool HasMidStatementTrailingComment(List<Token> tokens)
        {
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                var trailing = tokens[i].TrailingTrivia;
                if (trailing == null) continue;
                foreach (var t in trailing)
                {
                    if (t.Kind == TriviaKind.SingleLineComment ||
                        t.Kind == TriviaKind.MultiLineComment)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Writes expression tokens with proper spacing, keyword uppercasing, and type case.
        /// Newlines in leading trivia are skipped in expression context to prevent
        /// continuation lines from appearing at column 0.
        /// </summary>
        private void WriteExpressionTokens(List<Token> tokens, bool preserveMultiLine = true)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                var tok = tokens[i];
                var prev = i > 0 ? tokens[i - 1] : null;

                // Write leading trivia
                if (tok.LeadingTrivia != null)
                {
                    bool wroteNewline = false;
                    foreach (var trivia in tok.LeadingTrivia)
                    {
                        if (trivia.Kind == TriviaKind.NewLine)
                        {
                            if (preserveMultiLine)
                                wroteNewline = true;
                            continue;
                        }
                        if (trivia.Kind == TriviaKind.Whitespace)
                            continue;
                        if (trivia.Kind == TriviaKind.SingleLineComment ||
                            trivia.Kind == TriviaKind.MultiLineComment)
                        {
                            // Write any pending newline before comment
                            if (preserveMultiLine && wroteNewline && !_output.IsAtLineStart)
                            {
                                _output.WriteLine();
                                _output.WriteIndent(_indent.CurrentIndent + new string(' ', _options.IndentSize));
                                wroteNewline = false;
                            }
                            if (_output.IsAtLineStart)
                                _output.WriteIndent(_indent.CurrentIndent);
                            _output.Write(trivia.Text);
                            if (trivia.Kind == TriviaKind.SingleLineComment)
                            {
                                _output.WriteLine();
                                // Re-indent so the token after the comment keeps its
                                // indentation (otherwise it starts at column 0).
                                _output.WriteIndent(_indent.CurrentIndent);
                            }
                        }
                    }
                    // If there was a newline and we're not at line start, write continuation
                    if (preserveMultiLine && wroteNewline && !_output.IsAtLineStart)
                    {
                        _output.WriteLine();
                        _output.WriteIndent(_indent.CurrentIndent + new string(' ', _options.IndentSize));
                    }
                }

                // Max-line-length wrapping: before writing a token that follows a
                // comma at expression level, break onto a continuation line when the
                // current line is already at/over the configured limit. This keeps the
                // wrap inside the token emission loop, so it is CST-aware and idempotent.
                if (_options.MaxLineLength > 0
                    && prev != null
                    && (prev.Kind == TokenKind.Comma
                        || prev.Kind == TokenKind.OutputAssign)
                    && _output.CurrentLineLength >= _options.MaxLineLength - _options.IndentSize)
                {
                    _output.WriteLine();
                    _output.WriteIndent(_indent.CurrentIndent + _options.IndentString);
                }

                // Determine spacing before this token
                if (NeedsSpaceBefore(tok, prev, _options))
                    _output.Write(" ");

                WriteTokenFormatted(tok);
            }
        }

        /// <summary>
        /// Writes FOR loop expression tokens, ensuring TO and BY keywords are uppercased.
        /// </summary>
        private void WriteForExpressionTokens(List<Token> tokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                var tok = tokens[i];
                var prev = i > 0 ? tokens[i - 1] : null;

                WriteLeadingTrivia(tok);

                if (NeedsSpaceBefore(tok, prev, _options))
                    _output.Write(" ");

                // Special handling for TO and BY keywords in FOR
                if (tok.Kind == TokenKind.Keyword_To || tok.Kind == TokenKind.Keyword_By)
                    _output.WriteKeyword(tok.Text);
                else
                    WriteTokenFormatted(tok);
            }
        }

        #endregion

        #region Attribute Directive

        private void VisitAttributeDirective(AttributeDirective node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            foreach (var tok in node.Tokens)
            {
                WriteLeadingTrivia(tok);
                // Preserve pragma text exactly
                _output.Write(tok.Text);
                WriteTrailingTrivia(tok);
            }

            _output.WriteLine();
        }

        #endregion

        #region Type Declaration

        private void VisitTypeDeclaration(TypeDeclaration node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write TYPE keyword
            Token endTypeToken = null;
            Token semicolonToken = null;
            bool wroteFirst = false;

            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_EndType)
                {
                    endTypeToken = tok;
                    break;
                }

                // The terminating ';' of an enum (TYPE x : (a,b,c);) comes AFTER
                // the body in source order — save it and write it after children.
                if (tok.Kind == TokenKind.Semicolon)
                {
                    semicolonToken = tok;
                    continue;
                }

                WriteLeadingTrivia(tok);

                if (wroteFirst)
                    _output.Write(" ");

                if (tok.Kind == TokenKind.Keyword_Type)
                    _output.WriteKeyword("TYPE");
                else if (tok.Kind == TokenKind.BadToken && tok.Text == ":")
                    _output.Write(":");
                else
                    _output.Write(tok.Text);

                wroteFirst = true;
            }

            _output.WriteLine();

            // Visit body (STRUCT/ENUM/UNION)
            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }

            // Write the enum ';' after the body, before END_TYPE
            if (semicolonToken != null)
            {
                WriteLeadingTrivia(semicolonToken);
                _output.Write(";");
                WriteTrailingTrivia(semicolonToken);
                _output.WriteLine();
            }

            // Blank lines before END_TYPE
            for (int i = 0; i < _options.BlankLinesBeforeEnd; i++)
                _output.WriteLine();

            // Write END_TYPE
            if (endTypeToken != null)
            {
                WriteLeadingTrivia(endTypeToken);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_TYPE");
                WriteTrailingTrivia(endTypeToken);
            }
            _output.WriteLine();
        }

        private void VisitStructBody(StructBody node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write STRUCT keyword
            if (node.Tokens.Count > 0)
            {
                WriteLeadingTrivia(node.Tokens[0]);
                _output.WriteKeyword("STRUCT");
            }
            _output.WriteLine();

            // Calculate alignment for struct members
            int maxNameLen = 0;
            if (_options.AlignDeclarations)
            {
                foreach (var child in node.Children)
                {
                    if (child is VarDeclaration vd)
                        maxNameLen = Math.Max(maxNameLen, (vd.Name ?? "").Length);
                }
            }

            // Visit members
            foreach (var child in node.Children)
            {
                if (child is VarDeclaration vd)
                    VisitVarDeclaration(vd, maxNameLen);
                else
                    Visit(child);
            }

            // Write END_STRUCT
            var endTok = node.Tokens.LastOrDefault(t => t.Kind == TokenKind.Keyword_EndStruct);
            if (endTok != null)
            {
                WriteLeadingTrivia(endTok);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_STRUCT");
                WriteTrailingTrivia(endTok);
            }
            else
            {
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_STRUCT");
            }
            _output.WriteLine();
        }

        private void VisitEnumBody(EnumBody node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Enum bodies are frequently laid out one value per line. Reflowing
            // them into a single `( a, b, c )` line loses the original layout and,
            // worse, the per-token LeadingTrivia (newlines) interacts with the
            // inline writer below producing blank-line drift and stray semicolons.
            // When the original is multi-line, preserve it verbatim — the same
            // "don't touch what you can't safely reformat" policy as TcBlack.
            if (HasNewlineTrivia(node.Tokens))
            {
                WriteTokensVerbatim(node.Tokens);
                _output.WriteLine();
                return;
            }

            // Single-line enum: ( value1, value2, ... )
            for (int i = 0; i < node.Tokens.Count; i++)
            {
                var tok = node.Tokens[i];
                WriteLeadingTrivia(tok);

                if (tok.Kind == TokenKind.LeftParen)
                {
                    _output.Write("(");
                    continue;
                }
                if (tok.Kind == TokenKind.RightParen)
                {
                    _output.Write(")");
                    continue;
                }
                if (tok.Kind == TokenKind.Comma)
                {
                    _output.Write(", ");
                    continue;
                }

                // Write enum value or type token
                WriteTokenFormatted(tok);
            }

            _output.WriteLine();
        }

        /// <summary>
        /// True when any token carries a newline in its leading/trailing trivia,
        /// meaning the construct spans multiple physical lines in the source.
        /// </summary>
        private static bool HasNewlineTrivia(System.Collections.Generic.IEnumerable<Token> tokens)
        {
            foreach (var tok in tokens)
            {
                foreach (var t in tok.LeadingTrivia)
                    if (t.Kind == TriviaKind.NewLine) return true;
                foreach (var t in tok.TrailingTrivia)
                    if (t.Kind == TriviaKind.NewLine) return true;
            }
            return false;
        }

        private void VisitUnionBody(UnionBody node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write UNION keyword
            if (node.Tokens.Count > 0)
            {
                WriteLeadingTrivia(node.Tokens[0]);
                _output.WriteKeyword("UNION");
            }
            _output.WriteLine();

            // Calculate alignment for union members
            int maxNameLen = 0;
            if (_options.AlignDeclarations)
            {
                foreach (var child in node.Children)
                {
                    if (child is VarDeclaration vd)
                        maxNameLen = Math.Max(maxNameLen, (vd.Name ?? "").Length);
                }
            }

            // Visit members
            foreach (var child in node.Children)
            {
                if (child is VarDeclaration vd)
                    VisitVarDeclaration(vd, maxNameLen);
                else
                    Visit(child);
            }

            // Write END_UNION
            var endTok = node.Tokens.LastOrDefault(t => t.Kind == TokenKind.Keyword_EndUnion);
            if (endTok != null)
            {
                WriteLeadingTrivia(endTok);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_UNION");
                WriteTrailingTrivia(endTok);
            }
            else
            {
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_UNION");
            }
            _output.WriteLine();
        }

        #endregion

        #region Namespace and Using

        private void VisitNamespaceDeclaration(NamespaceDeclaration node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write NAMESPACE keyword and name
            Token endNsToken = null;
            bool wroteFirst = false;

            foreach (var tok in node.Tokens)
            {
                if (tok.Kind == TokenKind.Keyword_EndNamespace)
                {
                    endNsToken = tok;
                    break;
                }

                WriteLeadingTrivia(tok);

                if (wroteFirst)
                    _output.Write(" ");

                if (IsKeyword(tok.Kind))
                    _output.WriteKeyword(tok.Text);
                else
                    _output.Write(tok.Text);

                wroteFirst = true;
            }

            _output.WriteLine();

            // Visit body children
            using (_indent.Push())
            {
                VisitStatementList(node.Children);
            }

            // Blank lines before END_NAMESPACE
            for (int i = 0; i < _options.BlankLinesBeforeEnd; i++)
                _output.WriteLine();

            // Write END_NAMESPACE
            if (endNsToken != null)
            {
                WriteLeadingTrivia(endNsToken);
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_NAMESPACE");
                WriteTrailingTrivia(endNsToken);
            }
            else
            {
                _output.WriteIndent(_indent.CurrentIndent);
                _output.WriteKeyword("END_NAMESPACE");
            }
            _output.WriteLine();
        }

        private void VisitUsingDirective(UsingDirective node)
        {
            _output.WriteIndent(_indent.CurrentIndent);

            // Write USING keyword
            if (node.Tokens.Count > 0)
            {
                WriteLeadingTrivia(node.Tokens[0]);
                _output.WriteKeyword("USING");
                _output.Write(" ");
            }

            // Write namespace name
            _output.Write(node.NamespaceName ?? "");

            // Write semicolon
            var semiToken = node.Tokens.LastOrDefault(t => t.Kind == TokenKind.Semicolon);
            if (semiToken != null)
            {
                _output.Write(";");
                WriteTrailingTrivia(semiToken);
            }
            else
            {
                _output.Write(";");
            }

            _output.WriteLine();
        }

        #endregion

        #region Unknown Node

        private void VisitUnknownNode(UnknownNode node)
        {
            if (node.Tokens.Count == 0 && node.Children.Count == 0)
                return;

            _output.WriteIndent(_indent.CurrentIndent);

            // Opaque recovery: an UnknownNode holds raw tokens for a construct we do
            // not understand. Emit them byte-for-byte (original text + original
            // trivia) so unrecognized code is preserved exactly — the same policy
            // TcBlack applies to lines it cannot confidently reformat.
            WriteTokensVerbatim(node.Tokens);

            // Visit any children
            VisitStatementList(node.Children);

            _output.WriteLine();
        }

        #endregion

        #region Token Writing Helpers

        /// <summary>
        /// Writes a token with proper formatting (keywords uppercase, type case, etc.).
        /// </summary>
        private void WriteTokenFormatted(Token token)
        {
            if (IsTypeKeyword(token.Kind))
            {
                _output.Write(FormatTypeTokenText(token));
            }
            else if (IsKeyword(token.Kind))
            {
                _output.WriteKeyword(token.Text);
            }
            else if (token.Kind == TokenKind.StringLiteral || token.Kind == TokenKind.WStringLiteral)
            {
                _output.Write(token.Text); // Preserve strings exactly
            }
            else if (token.Kind == TokenKind.TypedLiteral)
            {
                _output.Write(token.Text); // Preserve typed literals
            }
            else if (token.Kind == TokenKind.Pragma)
            {
                _output.Write(token.Text); // Preserve pragmas exactly
            }
            else if (token.Kind == TokenKind.Identifier)
            {
                _output.Write(FormatTypeTokenText(token));
            }
            else
            {
                _output.Write(token.Text);
            }
        }

        /// <summary>
        /// Writes a type-position token applying TypeCase option.
        /// TypeCase only affects *built-in* type tokens: the standard type
        /// identifiers (BOOL, INT, STRING, ...) and the type keywords (ARRAY, OF,
        /// POINTER, REFERENCE, TO). Library/user-defined types such as I_TcMessage
        /// are identifiers that are NOT standard types, so they keep their original
        /// spelling exactly.
        /// </summary>
        private void WriteTypeToken(Token token)
        {
            if (IsTypeKeyword(token.Kind))
            {
                _output.Write(ApplyTypeCase(token.Text));
            }
            else if (token.Kind == TokenKind.Identifier)
            {
                if (StandardTypes.Contains(token.Text))
                    _output.Write(ApplyTypeCase(token.Text));
                else
                    _output.Write(token.Text);
            }
            else
            {
                _output.Write(token.Text);
            }
        }

        /// <summary>
        /// Applies TypeCase option to a token's text, but only for built-in types:
        /// standard type identifiers and the type keywords (ARRAY, OF, POINTER,
        /// REFERENCE, TO). Nothing else is touched.
        /// </summary>
        private string FormatTypeTokenText(Token token)
        {
            if (token.Kind == TokenKind.Identifier && StandardTypes.Contains(token.Text))
                return ApplyTypeCase(token.Text);
            if (IsTypeKeyword(token.Kind))
                return ApplyTypeCase(token.Text);
            return token.Text;
        }

        /// <summary>
        /// True for the keyword tokens that appear in type position (ARRAY, OF,
        /// POINTER, REFERENCE, TO). These are the "keyword types" TypeCase applies to.
        /// </summary>
        private static bool IsTypeKeyword(TokenKind kind)
        {
            return kind == TokenKind.Keyword_Array ||
                   kind == TokenKind.Keyword_Of ||
                   kind == TokenKind.Keyword_Pointer ||
                   kind == TokenKind.Keyword_Reference ||
                   kind == TokenKind.Keyword_To;
        }

        private string ApplyTypeCase(string text)
        {
            switch (_options.TypeCase)
            {
                case TypeCase.Upper: return text.ToUpperInvariant();
                case TypeCase.Lower: return text.ToLowerInvariant();
                default: return text;
            }
        }

        #endregion

        #region Trivia Handling

        /// <summary>
        /// Writes leading trivia (comments, newlines) before a token.
        /// </summary>
        private void WriteLeadingTrivia(Token token)
        {
            if (token.LeadingTrivia == null || token.LeadingTrivia.Count == 0)
                return;

            foreach (var trivia in token.LeadingTrivia)
            {
                switch (trivia.Kind)
                {
                    case TriviaKind.NewLine:
                        // A lone newline is the structural line break written by the
                        // visit methods, so it must not be re-emitted here. But two
                        // or more consecutive newlines mean an *intentional blank
                        // line* in the source; preserve it only when KeepEmptyLines
                        // is true (that option means "keep existing blank lines",
                        // not "add new ones"). After the blank line the following
                        // token starts a new line, so re-apply the current indent —
                        // otherwise the token would start at column 0.
                        if (_options.KeepEmptyLines)
                        {
                            int newlines = 1;
                            int idx = token.LeadingTrivia.IndexOf(trivia);
                            for (int k = idx + 1; k < token.LeadingTrivia.Count; k++)
                            {
                                if (token.LeadingTrivia[k].Kind == TriviaKind.NewLine)
                                    newlines++;
                                else
                                    break;
                            }
                            if (newlines >= 2)
                            {
                                _output.WriteBlankLine();
                                _output.WriteIndent(_indent.CurrentIndent);
                            }
                        }
                        break;
                    case TriviaKind.SingleLineComment:
                    case TriviaKind.MultiLineComment:
                        // A comment that travelled as leading trivia sits on its own
                        // line directly above the token. Emit it at the current
                        // indent, end the line, and re-indent so the token itself
                        // keeps its indentation (otherwise the token would start the
                        // next line at column 0).
                        if (_output.IsAtLineStart)
                            _output.WriteIndent(_indent.CurrentIndent);
                        _output.Write(trivia.Text);
                        _output.WriteLine();
                        _output.WriteIndent(_indent.CurrentIndent);
                        break;
                    case TriviaKind.Whitespace:
                        break;
                }
            }
        }

        /// <summary>
        /// Writes trailing trivia (inline comments) after a token.
        /// </summary>
        private void WriteTrailingTrivia(Token token)
        {
            if (token.TrailingTrivia == null || token.TrailingTrivia.Count == 0)
                return;

            foreach (var trivia in token.TrailingTrivia)
            {
                switch (trivia.Kind)
                {
                    case TriviaKind.SingleLineComment:
                        _output.Write(" ");
                        _output.Write(trivia.Text);
                        break;
                    case TriviaKind.MultiLineComment:
                        _output.Write(" ");
                        _output.Write(trivia.Text);
                        break;
                    case TriviaKind.NewLine:
                        // Skip newlines in trailing trivia - the explicit WriteLine()
                        // calls in visit methods handle line endings. This prevents
                        // duplicate blank lines from appearing in the output.
                        break;
                }
            }
        }

        #endregion

        #region Spacing Rules

        /// <summary>
        /// Determines if a space is needed before the current token.
        /// </summary>
        private static bool NeedsSpaceBefore(Token current, Token previous, FormatterOptions opts)
        {
            if (previous == null) return false;

            // No space after left paren/bracket when ParenInnerSpacing is false
            if (!opts.ParenInnerSpacing &&
                (previous.Kind == TokenKind.LeftParen || previous.Kind == TokenKind.LeftBracket))
                return false;

            // No space before right paren/bracket when ParenInnerSpacing is false
            if (!opts.ParenInnerSpacing &&
                (current.Kind == TokenKind.RightParen || current.Kind == TokenKind.RightBracket))
                return false;

            // Comma spacing: no space before comma, space after
            if (current.Kind == TokenKind.Comma)
                return false;
            if (previous.Kind == TokenKind.Comma)
                return opts.CommaSpacing;

            // No space before semicolon
            if (current.Kind == TokenKind.Semicolon)
                return false;

            // No space around dot
            if (current.Kind == TokenKind.Dot || previous.Kind == TokenKind.Dot)
                return false;

            // No space around '#' (enum value access E_Mode#Running, typed
            // literals on user-defined types). Standard-type typed literals
            // (INT#5) are already scanned as single TypedLiteral tokens.
            if (current.Kind == TokenKind.Hash || previous.Kind == TokenKind.Hash)
                return false;

            // No space around caret (dereference)
            if (current.Kind == TokenKind.Caret || previous.Kind == TokenKind.Caret)
                return false;

            // No space before ( when preceded by Identifier (function call): Func(args) not Func (args)
            if (current.Kind == TokenKind.LeftParen && previous.Kind == TokenKind.Identifier)
                return false;

            // No space before [ when preceded by Identifier (array access): arr[0] not arr [0]
            if (current.Kind == TokenKind.LeftBracket && previous.Kind == TokenKind.Identifier)
                return false;

            // No space before [ when preceded by RightBracket (multi-dimensional): arr[0][1]
            if (current.Kind == TokenKind.LeftBracket && previous.Kind == TokenKind.RightBracket)
                return false;

            // OutputAssign (=>) gets operator spacing
            if (current.Kind == TokenKind.OutputAssign || previous.Kind == TokenKind.OutputAssign)
                return opts.OperatorSpacing;

            // Operator spacing
            if (opts.OperatorSpacing)
            {
                if (IsOperator(current.Kind) || IsOperator(previous.Kind))
                    return true;
            }

            // Default: space between most tokens
            return true;
        }

        private static bool IsOperator(TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.Plus:
                case TokenKind.Minus:
                case TokenKind.Star:
                case TokenKind.Slash:
                case TokenKind.StarStar:
                case TokenKind.Assign:
                case TokenKind.OutputAssign:
                case TokenKind.RefAssign:
                case TokenKind.Equal:
                case TokenKind.NotEqual:
                case TokenKind.LessThan:
                case TokenKind.GreaterThan:
                case TokenKind.LessEqual:
                case TokenKind.GreaterEqual:
                case TokenKind.Keyword_And:
                case TokenKind.Keyword_Or:
                case TokenKind.Keyword_Xor:
                case TokenKind.Keyword_Not:
                case TokenKind.Keyword_AndThen:
                case TokenKind.Keyword_OrElse:
                case TokenKind.Keyword_Mod:
                case TokenKind.Keyword_Expt:
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        #region Utility

        private static bool IsKeyword(TokenKind kind)
        {
            return kind >= TokenKind.Keyword_Program && kind <= TokenKind.Keyword_False;
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

        /// <summary>
        /// Removes trailing whitespace from each line.
        /// </summary>
        private string RemoveTrailingWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var sb = new System.Text.StringBuilder(text.Length);
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append(lines[i].TrimEnd(' ', '\t'));
                if (i < lines.Length - 1)
                    sb.Append(_lineEnding);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Collapses runs of blank lines to at most one (idempotent fixed point).
        /// Blank-line *removal* for KeepEmptyLines=false happens earlier, at emit
        /// time: source blank lines are only written when KeepEmptyLines is true,
        /// while structural blank lines around statement blocks (IF/CASE/FOR/
        /// WHILE/REPEAT) are always written. So this pass only needs to dedup.
        /// </summary>
        private string NormalizeBlankLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Collapse runs of blank lines to at most one (idempotent).
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var collapsed = new System.Collections.Generic.List<string>();
            int consecutiveEmpty = 0;
            foreach (var line in lines)
            {
                bool empty = line.Trim(' ', '\t').Length == 0;
                if (empty)
                {
                    consecutiveEmpty++;
                    if (consecutiveEmpty > 1) continue; // drop extra blank line
                }
                else
                {
                    consecutiveEmpty = 0;
                }
                collapsed.Add(line);
            }
            text = string.Join(_lineEnding, collapsed);

            return text;
        }

        #endregion
    }
}
