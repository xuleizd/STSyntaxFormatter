using System;
using System.Collections.Generic;

namespace STFormatterCore.Lexer
{
    public sealed class STLexer
    {
        private readonly string _source;
        private int _pos;
        private readonly List<Token> _tokens;

        private static readonly Dictionary<string, TokenKind> Keywords =
            new Dictionary<string, TokenKind>(StringComparer.OrdinalIgnoreCase)
            {
                // POU declarations
                { "PROGRAM", TokenKind.Keyword_Program },
                { "END_PROGRAM", TokenKind.Keyword_EndProgram },
                { "FUNCTION", TokenKind.Keyword_Function },
                { "END_FUNCTION", TokenKind.Keyword_EndFunction },
                { "FUNCTION_BLOCK", TokenKind.Keyword_FunctionBlock },
                { "END_FUNCTION_BLOCK", TokenKind.Keyword_EndFunctionBlock },

                // VAR blocks
                { "VAR", TokenKind.Keyword_Var },
                { "END_VAR", TokenKind.Keyword_EndVar },
                { "VAR_INPUT", TokenKind.Keyword_VarInput },
                { "VAR_OUTPUT", TokenKind.Keyword_VarOutput },
                { "VAR_IN_OUT", TokenKind.Keyword_VarInOut },
                { "VAR_TEMP", TokenKind.Keyword_VarTemp },
                { "VAR_STAT", TokenKind.Keyword_VarStat },
                { "VAR_INST", TokenKind.Keyword_VarInst },
                { "VAR_CONSTANT", TokenKind.Keyword_VarConstant },
                { "VAR_GLOBAL", TokenKind.Keyword_VarGlobal },
                { "VAR_RETAIN", TokenKind.Keyword_VarRetain },
                { "VAR_PERSISTENT", TokenKind.Keyword_VarPersistent },
                { "VAR_EXTERNAL", TokenKind.Keyword_VarExternal },
                { "VAR_ACCESS", TokenKind.Keyword_VarAccess },
                { "VAR_CONFIG", TokenKind.Keyword_VarConfig },

                // VAR modifiers
                { "CONSTANT", TokenKind.Keyword_Constant },
                { "RETAIN", TokenKind.Keyword_Retain },
                { "PERSISTENT", TokenKind.Keyword_Persistent },

                // Type declarations
                { "TYPE", TokenKind.Keyword_Type },
                { "END_TYPE", TokenKind.Keyword_EndType },
                { "STRUCT", TokenKind.Keyword_Struct },
                { "END_STRUCT", TokenKind.Keyword_EndStruct },
                { "UNION", TokenKind.Keyword_Union },
                { "END_UNION", TokenKind.Keyword_EndUnion },
                { "ARRAY", TokenKind.Keyword_Array },
                { "OF", TokenKind.Keyword_Of },
                { "POINTER", TokenKind.Keyword_Pointer },
                { "REFERENCE", TokenKind.Keyword_Reference },
                { "TO", TokenKind.Keyword_To },
                { "AT", TokenKind.Keyword_At },

                // Control flow
                { "IF", TokenKind.Keyword_If },
                { "THEN", TokenKind.Keyword_Then },
                { "ELSIF", TokenKind.Keyword_Elsif },
                { "ELSE", TokenKind.Keyword_Else },
                { "END_IF", TokenKind.Keyword_EndIf },
                { "CASE", TokenKind.Keyword_Case },
                { "END_CASE", TokenKind.Keyword_EndCase },
                { "FOR", TokenKind.Keyword_For },
                { "BY", TokenKind.Keyword_By },
                { "DO", TokenKind.Keyword_Do },
                { "END_FOR", TokenKind.Keyword_EndFor },
                { "WHILE", TokenKind.Keyword_While },
                { "END_WHILE", TokenKind.Keyword_EndWhile },
                { "REPEAT", TokenKind.Keyword_Repeat },
                { "UNTIL", TokenKind.Keyword_Until },
                { "END_REPEAT", TokenKind.Keyword_EndRepeat },
                { "EXIT", TokenKind.Keyword_Exit },
                { "CONTINUE", TokenKind.Keyword_Continue },
                { "RETURN", TokenKind.Keyword_Return },

                // Word operators
                { "AND", TokenKind.Keyword_And },
                { "OR", TokenKind.Keyword_Or },
                { "XOR", TokenKind.Keyword_Xor },
                { "NOT", TokenKind.Keyword_Not },
                { "AND_THEN", TokenKind.Keyword_AndThen },
                { "OR_ELSE", TokenKind.Keyword_OrElse },
                { "MOD", TokenKind.Keyword_Mod },
                { "EXPT", TokenKind.Keyword_Expt },

                // OOP
                { "EXTENDS", TokenKind.Keyword_Extends },
                { "IMPLEMENTS", TokenKind.Keyword_Implements },
                { "METHOD", TokenKind.Keyword_Method },
                { "END_METHOD", TokenKind.Keyword_EndMethod },
                { "PROPERTY", TokenKind.Keyword_Property },
                { "END_PROPERTY", TokenKind.Keyword_EndProperty },
                { "INTERFACE", TokenKind.Keyword_Interface },
                { "END_INTERFACE", TokenKind.Keyword_EndInterface },
                { "ABSTRACT", TokenKind.Keyword_Abstract },
                { "FINAL", TokenKind.Keyword_Final },
                { "OVERRIDE", TokenKind.Keyword_Override },
                { "PUBLIC", TokenKind.Keyword_Public },
                { "PRIVATE", TokenKind.Keyword_Private },
                { "PROTECTED", TokenKind.Keyword_Protected },
                { "INTERNAL", TokenKind.Keyword_Internal },
                { "THIS", TokenKind.Keyword_This },
                { "SUPER", TokenKind.Keyword_Super },

                // Configuration
                { "CONFIGURATION", TokenKind.Keyword_Configuration },
                { "END_CONFIGURATION", TokenKind.Keyword_EndConfiguration },
                { "RESOURCE", TokenKind.Keyword_Resource },
                { "END_RESOURCE", TokenKind.Keyword_EndResource },
                { "TASK", TokenKind.Keyword_Task },
                { "WITH", TokenKind.Keyword_With },
                { "NON_RETAIN", TokenKind.Keyword_NonRetain },

                // Other keywords
                { "GET", TokenKind.Keyword_Get },
                { "SET", TokenKind.Keyword_Set },
                { "NEW", TokenKind.Keyword_New },
                { "DELETE", TokenKind.Keyword_Delete },
                { "REF_TO", TokenKind.Keyword_RefTo },
                { "NAMESPACE", TokenKind.Keyword_Namespace },
                { "END_NAMESPACE", TokenKind.Keyword_EndNamespace },
                { "USING", TokenKind.Keyword_Using },
                { "ACTION", TokenKind.Keyword_Action },
                { "END_ACTION", TokenKind.Keyword_EndAction },
                { "TRANSITION", TokenKind.Keyword_Transition },
                { "END_TRANSITION", TokenKind.Keyword_EndTransition },
                { "TRUE", TokenKind.Keyword_True },
                { "FALSE", TokenKind.Keyword_False },
            };

        private static readonly HashSet<string> TypedLiteralPrefixes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "T", "TIME", "D", "DATE", "DT", "DATE_AND_TIME",
                "TOD", "TIME_OF_DAY",
                "LT", "LTIME", "LD", "LDATE", "LDT", "LTOD",
                // Typed literals on elementary types (IEC 61131-3 / TwinCAT):
                // INT#5, WORD#16#FF, BOOL#TRUE, REAL#3.14 ...
                "BOOL", "BYTE", "WORD", "DWORD", "LWORD",
                "SINT", "INT", "DINT", "LINT",
                "USINT", "UINT", "UDINT", "ULINT",
                "REAL", "LREAL",
            };

        public STLexer(string source)
        {
            // Normalize line endings to \n to prevent \r from becoming BadTokens
            _source = (source ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            _pos = 0;
            _tokens = new List<Token>();
        }

        #region Character Helpers

        private char Peek(int offset = 0)
        {
            int idx = _pos + offset;
            return idx < _source.Length ? _source[idx] : '\0';
        }

        private char Advance()
        {
            char c = _source[_pos];
            _pos++;
            return c;
        }

        private bool IsAtEnd => _pos >= _source.Length;

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        private static bool IsIdentStart(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

        private static bool IsIdentPart(char c) =>
            IsIdentStart(c) || IsDigit(c);

        #endregion

        #region Public API

        public List<Token> Tokenize()
        {
            var pendingTrivia = new List<Trivia>();

            while (true)
            {
                pendingTrivia.Clear();
                ScanTrivia(pendingTrivia, out bool hasNewline);

                if (IsAtEnd)
                {
                    _tokens.Add(new Token(TokenKind.EndOfFile, string.Empty, _pos,
                        new List<Trivia>(pendingTrivia)));
                    break;
                }

                // Categorize trivia: anything on the same line (before the first
                // newline) belongs to the previous token as TRAILING trivia;
                // the first newline and everything after it becomes LEADING trivia
                // of the token we are about to scan. This keeps "x := 1; // comment"
                // attached to the previous line even though the newline after the
                // comment was consumed in the same pass.
                if (pendingTrivia.Count > 0)
                {
                    int firstNewline = -1;
                    for (int i = 0; i < pendingTrivia.Count; i++)
                    {
                        if (pendingTrivia[i].Kind == TriviaKind.NewLine)
                        {
                            firstNewline = i;
                            break;
                        }
                    }

                    if (firstNewline >= 0)
                    {
                        // Move pre-newline trivia to previous token's trailing trivia —
                        // but only when a previous token exists. Leading comments at the
                        // very start of the source must stay with the current token.
                        if (_tokens.Count > 0 && firstNewline > 0)
                        {
                            for (int i = 0; i < firstNewline; i++)
                                _tokens[_tokens.Count - 1].TrailingTrivia.Add(pendingTrivia[i]);
                        }
                        else if (_tokens.Count == 0)
                        {
                            // No previous token: everything stays leading.
                            firstNewline = 0;
                        }

                        // Keep the newline and everything after it as leading trivia.
                        var leading = new List<Trivia>();
                        for (int i = firstNewline; i < pendingTrivia.Count; i++)
                            leading.Add(pendingTrivia[i]);
                        pendingTrivia.Clear();
                        pendingTrivia.AddRange(leading);
                    }
                    else if (_tokens.Count > 0)
                    {
                        // Same-line trivia → trailing trivia of previous token.
                        _tokens[_tokens.Count - 1].TrailingTrivia.AddRange(pendingTrivia);
                        pendingTrivia.Clear();
                    }
                }

                int tokenStart = _pos;
                TokenKind kind = ScanToken();
                string text = _source.Substring(tokenStart, _pos - tokenStart);

                // pendingTrivia (if not cleared) becomes LeadingTrivia.
                _tokens.Add(new Token(kind, text, tokenStart,
                    pendingTrivia.Count > 0 ? new List<Trivia>(pendingTrivia) : null));
            }

            return _tokens;
        }

        #endregion

        #region Trivia Scanning

        /// <summary>
        /// Scans whitespace, newlines, and comments into <paramref name="trivia"/>.
        /// Sets <paramref name="hasNewline"/> if any newline was encountered.
        /// </summary>
        private void ScanTrivia(List<Trivia> trivia, out bool hasNewline)
        {
            hasNewline = false;

            while (!IsAtEnd)
            {
                char c = Peek();

                // Whitespace (not newlines)
                if (c == ' ' || c == '\t')
                {
                    int start = _pos;
                    while (!IsAtEnd && (Peek() == ' ' || Peek() == '\t'))
                        _pos++;
                    trivia.Add(new Trivia(TriviaKind.Whitespace, _source.Substring(start, _pos - start)));
                    continue;
                }

                // Newlines
                if (c == '\r' || c == '\n')
                {
                    int start = _pos;
                    if (c == '\r' && Peek(1) == '\n')
                        _pos += 2;
                    else if (c == '\r')
                        _pos++; // standalone CR (old Mac line ending)
                    else
                        _pos++;
                    trivia.Add(new Trivia(TriviaKind.NewLine, _source.Substring(start, _pos - start)));
                    hasNewline = true;
                    continue;
                }

                // Single-line comment: //
                if (c == '/' && Peek(1) == '/')
                {
                    int start = _pos;
                    ScanSingleLineComment();
                    trivia.Add(new Trivia(TriviaKind.SingleLineComment, _source.Substring(start, _pos - start)));
                    // IMPORTANT: continue (not break) so the trailing newline after
                    // the comment is scanned as NewLine trivia in this same pass.
                    // Breaking here leaked the newline into the next ScanToken()
                    // call, producing a BadToken whose text contained '\n'.
                    continue;
                }

                // IEC 61131-3 multi-line comment: (* ... *) with nesting
                if (c == '(' && Peek(1) == '*')
                {
                    int start = _pos;
                    ScanIecComment();
                    trivia.Add(new Trivia(TriviaKind.MultiLineComment, _source.Substring(start, _pos - start)));
                    continue;
                }

                // C-style multi-line comment: /* ... */ (TwinCAT extension, no nesting)
                if (c == '/' && Peek(1) == '*')
                {
                    int start = _pos;
                    ScanCComment();
                    trivia.Add(new Trivia(TriviaKind.MultiLineComment, _source.Substring(start, _pos - start)));
                    continue;
                }

                break; // Not trivia
            }
        }

        /// <summary>Scans // comment. Position is at the first /.</summary>
        private void ScanSingleLineComment()
        {
            _pos += 2; // skip //
            while (!IsAtEnd && Peek() != '\r' && Peek() != '\n')
                _pos++;
        }

        /// <summary>Scans (* ... *) comment with nesting support. Position is at (.</summary>
        private void ScanIecComment()
        {
            _pos += 2; // skip (*
            int depth = 1;

            while (!IsAtEnd && depth > 0)
            {
                if (Peek() == '(' && Peek(1) == '*')
                {
                    _pos += 2;
                    depth++;
                }
                else if (Peek() == '*' && Peek(1) == ')')
                {
                    _pos += 2;
                    depth--;
                }
                else
                {
                    _pos++;
                }
            }
        }

        /// <summary>Scans /* ... */ comment (no nesting). Position is at /.</summary>
        private void ScanCComment()
        {
            _pos += 2; // skip /*
            while (!IsAtEnd)
            {
                if (Peek() == '*' && Peek(1) == '/')
                {
                    _pos += 2;
                    return;
                }
                _pos++;
            }
        }

        #endregion

        #region Token Scanning

        /// <summary>Scans a single token. Position is at the first character of the token.</summary>
        private TokenKind ScanToken()
        {
            char c = Peek();

            switch (c)
            {
                case '(':
                    // Check for (* comment that wasn't caught by ScanTrivia
                    // (happens when ( immediately follows a token with no whitespace)
                    if (Peek(1) == '*')
                    {
                        var trivia = new List<Trivia>();
                        int start = _pos;
                        ScanIecComment();
                        trivia.Add(new Trivia(TriviaKind.MultiLineComment,
                            _source.Substring(start, _pos - start)));
                        // Attach as trailing trivia of previous token
                        if (_tokens.Count > 0)
                            _tokens[_tokens.Count - 1].TrailingTrivia.AddRange(trivia);
                        return ScanToken(); // Recurse to get the actual next token
                    }
                    _pos++;
                    return TokenKind.LeftParen;

                case ')': _pos++; return TokenKind.RightParen;
                case '[': _pos++; return TokenKind.LeftBracket;
                case ']': _pos++; return TokenKind.RightBracket;
                case ';': _pos++; return TokenKind.Semicolon;
                case ',': _pos++; return TokenKind.Comma;
                case '^': _pos++; return TokenKind.Caret;
                case '#': _pos++; return TokenKind.Hash;

                // Stray line-ending characters that weren't caught by ScanTrivia
                // (e.g., when immediately after a token with no whitespace).
                // Treat as trivia, not as BadTokens.
                case '\r':
                    _pos++;
                    if (Peek() == '\n') _pos++;
                    return ScanToken();
                case '\n':
                    _pos++;
                    return ScanToken();

                case '{':
                    return ScanPragma();

                case '%':
                    return ScanDirectVariableAddress();

                case '\'':
                    return ScanStringLiteral();

                case '"':
                    return ScanWStringLiteral();

                case ':':
                    if (Peek(1) == '=')
                    {
                        _pos += 2;
                        return TokenKind.Assign;
                    }
                    // Lone ':' is not a valid ST token
                    _pos++;
                    return TokenKind.BadToken;

                case '=':
                    if (Peek(1) == '>')
                    {
                        _pos += 2;
                        return TokenKind.OutputAssign;
                    }
                    _pos++;
                    return TokenKind.Equal;

                case '+': _pos++; return TokenKind.Plus;

                case '-': _pos++; return TokenKind.Minus;

                case '*':
                    if (Peek(1) == '*')
                    {
                        _pos += 2;
                        return TokenKind.StarStar;
                    }
                    _pos++;
                    return TokenKind.Star;

                case '/':
                    // Handle // or /* that weren't caught by ScanTrivia
                    // (happens when / immediately follows a token with no whitespace)
                    if (Peek(1) == '/')
                    {
                        var trivia = new List<Trivia>();
                        int start = _pos;
                        ScanSingleLineComment();
                        trivia.Add(new Trivia(TriviaKind.SingleLineComment,
                            _source.Substring(start, _pos - start)));
                        if (_tokens.Count > 0)
                            _tokens[_tokens.Count - 1].TrailingTrivia.AddRange(trivia);
                        return ScanToken(); // Recurse
                    }
                    if (Peek(1) == '*')
                    {
                        var trivia = new List<Trivia>();
                        int start = _pos;
                        ScanCComment();
                        trivia.Add(new Trivia(TriviaKind.MultiLineComment,
                            _source.Substring(start, _pos - start)));
                        if (_tokens.Count > 0)
                            _tokens[_tokens.Count - 1].TrailingTrivia.AddRange(trivia);
                        return ScanToken(); // Recurse
                    }
                    _pos++;
                    return TokenKind.Slash;

                case '<':
                    if (Peek(1) == '=')
                    {
                        _pos += 2;
                        return TokenKind.LessEqual;
                    }
                    if (Peek(1) == '>')
                    {
                        _pos += 2;
                        return TokenKind.NotEqual;
                    }
                    _pos++;
                    return TokenKind.LessThan;

                case '>':
                    if (Peek(1) == '=')
                    {
                        _pos += 2;
                        return TokenKind.GreaterEqual;
                    }
                    _pos++;
                    return TokenKind.GreaterThan;

                case '.':
                    if (Peek(1) == '.')
                    {
                        _pos += 2;
                        return TokenKind.DotDot;
                    }
                    _pos++;
                    return TokenKind.Dot;

                default:
                    if (IsDigit(c))
                        return ScanNumber();
                    if (IsIdentStart(c))
                        return ScanIdentifierOrKeyword();
                    _pos++;
                    return TokenKind.BadToken;
            }
        }

        #endregion

        #region Identifiers & Keywords

        private TokenKind ScanIdentifierOrKeyword()
        {
            int start = _pos;
            _pos++; // first char (letter or _)
            while (!IsAtEnd && IsIdentPart(Peek()))
                _pos++;

            string text = _source.Substring(start, _pos - start);

            // Check for REF= (RefAssign operator)
            if (text.Equals("REF", StringComparison.OrdinalIgnoreCase) && Peek() == '=')
            {
                _pos++; // consume '='
                return TokenKind.RefAssign;
            }

            // Check for typed literal prefix followed by #
            if (Peek() == '#' && TypedLiteralPrefixes.Contains(text))
            {
                _pos++; // consume '#'
                // Consume the literal value until whitespace, semicolon, or bracket
                // Also stop at ')' and ',' which are delimiters in function call contexts
                while (!IsAtEnd && Peek() != ' ' && Peek() != '\t'
                       && Peek() != '\r' && Peek() != '\n' && Peek() != ';'
                       && Peek() != ')' && Peek() != ',' && Peek() != '(')
                {
                    _pos++;
                }
                return TokenKind.TypedLiteral;
            }

            // Check for keyword
            if (Keywords.TryGetValue(text, out TokenKind keywordKind))
                return keywordKind;

            return TokenKind.Identifier;
        }

        #endregion

        #region Numeric Literals

        private TokenKind ScanNumber()
        {
            int start = _pos;

            // Consume leading digits
            while (!IsAtEnd && IsDigit(Peek()))
                _pos++;

            // Check for based literal: digits followed by #
            if (Peek() == '#')
            {
                _pos++; // consume '#'
                // Consume based-literal digits (hex digits + underscores)
                while (!IsAtEnd && (IsHexDigit(Peek()) || Peek() == '_'))
                    _pos++;
                return TokenKind.IntegerLiteral;
            }

            // Check for real literal: digits followed by . and another digit
            if (Peek() == '.' && Peek(1) != '.' && IsDigit(Peek(1)))
            {
                _pos++; // consume '.'
                while (!IsAtEnd && IsDigit(Peek()))
                    _pos++;

                // Exponent part
                if (Peek() == 'E' || Peek() == 'e')
                {
                    _pos++;
                    if (Peek() == '+' || Peek() == '-')
                        _pos++;
                    while (!IsAtEnd && IsDigit(Peek()))
                        _pos++;
                }

                return TokenKind.RealLiteral;
            }

            // Check for exponent without fractional part: e.g. 3E5
            if ((Peek() == 'E' || Peek() == 'e') && Peek(1) != '_')
            {
                // Make sure it's not an identifier starting with E
                char afterE = Peek(1);
                if (IsDigit(afterE) || afterE == '+' || afterE == '-')
                {
                    _pos++; // consume 'E'
                    if (Peek() == '+' || Peek() == '-')
                        _pos++;
                    if (!IsAtEnd && IsDigit(Peek()))
                    {
                        while (!IsAtEnd && IsDigit(Peek()))
                            _pos++;
                        return TokenKind.RealLiteral;
                    }
                    // No digits after exponent sign → not a valid real, backtrack not needed
                    // (we already consumed E and optional sign; produce real literal anyway)
                    return TokenKind.RealLiteral;
                }
            }

            return TokenKind.IntegerLiteral;
        }

        #endregion

        #region String Literals

        /// <summary>Scans a STRING literal delimited by single quotes. $ is the escape character.</summary>
        private TokenKind ScanStringLiteral()
        {
            _pos++; // skip opening '
            while (!IsAtEnd)
            {
                char c = Peek();
                if (c == '$' && _pos + 1 < _source.Length)
                {
                    _pos += 2; // skip escape sequence
                    continue;
                }
                if (c == '\'')
                {
                    // Doubled quote '' is an embedded literal quote (IEC 61131-3),
                    // not the end of the string.
                    if (Peek(1) == '\'')
                    {
                        _pos += 2;
                        continue;
                    }
                    _pos++; // skip closing '
                    return TokenKind.StringLiteral;
                }
                if (c == '\r' || c == '\n')
                    break; // Unterminated string at newline
                _pos++;
            }
            return TokenKind.StringLiteral; // Unterminated string
        }

        /// <summary>Scans a WSTRING literal delimited by double quotes. $ is the escape character.</summary>
        private TokenKind ScanWStringLiteral()
        {
            _pos++; // skip opening "
            while (!IsAtEnd)
            {
                char c = Peek();
                if (c == '$' && _pos + 1 < _source.Length)
                {
                    _pos += 2; // skip escape sequence
                    continue;
                }
                if (c == '"')
                {
                    // Doubled quote "" is an embedded literal quote, not the end.
                    if (Peek(1) == '"')
                    {
                        _pos += 2;
                        continue;
                    }
                    _pos++; // skip closing "
                    return TokenKind.WStringLiteral;
                }
                if (c == '\r' || c == '\n')
                    break; // Unterminated string at newline
                _pos++;
            }
            return TokenKind.WStringLiteral; // Unterminated string
        }

        #endregion

        #region Pragma / Attribute

        /// <summary>
        /// Scans a pragma/attribute token {...}.
        /// Handles nested braces and string literals inside pragmas.
        /// Position is at the opening {.
        /// </summary>
        private TokenKind ScanPragma()
        {
            _pos++; // skip opening {
            int depth = 1;

            while (!IsAtEnd && depth > 0)
            {
                char c = Peek();
                if (c == '{')
                {
                    depth++;
                    _pos++;
                }
                else if (c == '}')
                {
                    depth--;
                    _pos++;
                }
                else if (c == '\'')
                {
                    // Skip string literal inside pragma
                    _pos++;
                    while (!IsAtEnd && Peek() != '\'')
                    {
                        if (Peek() == '$' && _pos + 1 < _source.Length)
                            _pos += 2;
                        else
                            _pos++;
                    }
                    if (!IsAtEnd) _pos++; // skip closing '
                }
                else if (c == '"')
                {
                    // Skip wstring literal inside pragma
                    _pos++;
                    while (!IsAtEnd && Peek() != '"')
                    {
                        if (Peek() == '$' && _pos + 1 < _source.Length)
                            _pos += 2;
                        else
                            _pos++;
                    }
                    if (!IsAtEnd) _pos++; // skip closing "
                }
                else
                {
                    _pos++;
                }
            }

            return TokenKind.Pragma;
        }

        #endregion

        #region Direct Address

        /// <summary>
        /// Scans a direct (hardware) address token: %IX0.0, %QX0.1, %MW10, %MX0.0, %I*, %QX*, etc.
        /// Position is at the '%'.
        /// </summary>
        private TokenKind ScanDirectVariableAddress()
        {
            _pos++; // skip '%'

            // Optional location prefix: I, Q, M (case-insensitive)
            if (!IsAtEnd)
            {
                char c = Peek();
                if (c == 'I' || c == 'i' || c == 'Q' || c == 'q' || c == 'M' || c == 'm')
                    _pos++;
            }

            // Optional size prefix: X, B, W, D, L (case-insensitive)
            if (!IsAtEnd)
            {
                char c = Peek();
                if (c == 'X' || c == 'x' || c == 'B' || c == 'b' ||
                    c == 'W' || c == 'w' || c == 'D' || c == 'd' || c == 'L' || c == 'l')
                    _pos++;
            }

            // Numeric part: digits, dots between digit groups, and * wildcard
            while (!IsAtEnd)
            {
                char c = Peek();
                if (IsDigit(c))
                {
                    _pos++;
                }
                else if (c == '.' && _pos + 1 < _source.Length && IsDigit(_source[_pos + 1]))
                {
                    _pos++; // consume dot
                    // digits after dot consumed in next iteration
                }
                else if (c == '*')
                {
                    _pos++;
                    break; // wildcard ends the address
                }
                else
                {
                    break;
                }
            }

            return TokenKind.DirectAddress;
        }

        #endregion
    }
}
