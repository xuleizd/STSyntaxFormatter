namespace STFormatterCore.Lexer
{
    public enum TokenKind
    {
        // Special
        EndOfFile,
        BadToken,

        // Literals
        IntegerLiteral,     // 42, 16#FF, 2#1010
        RealLiteral,        // 3.14, 1.0E-5
        StringLiteral,      // 'hello'
        WStringLiteral,     // "hello"
        TypedLiteral,       // T#1s, TIME#1s500ms, DATE#2023-01-01, DT#..., TOD#..., etc.
        DirectAddress,      // %IX0.0, %QX0.1, %MW10, %MX0.0, %I*, %QX*

        // Identifiers
        Identifier,

        // Keywords - POU declarations
        Keyword_Program, Keyword_EndProgram,
        Keyword_Function, Keyword_EndFunction,
        Keyword_FunctionBlock, Keyword_EndFunctionBlock,

        // Keywords - VAR blocks
        Keyword_Var, Keyword_EndVar,
        Keyword_VarInput, Keyword_VarOutput, Keyword_VarInOut,
        Keyword_VarTemp, Keyword_VarStat, Keyword_VarInst,
        Keyword_VarConstant, Keyword_VarGlobal,
        Keyword_VarExternal, Keyword_VarAccess, Keyword_VarConfig,

        // Keywords - VAR modifiers
        Keyword_Constant, Keyword_Retain, Keyword_Persistent,

        // Keywords - Type declarations
        Keyword_Type, Keyword_EndType,
        Keyword_Struct, Keyword_EndStruct,
        Keyword_Union, Keyword_EndUnion,
        Keyword_Array, Keyword_Of,
        Keyword_Pointer, Keyword_Reference, Keyword_To,
        Keyword_At,

        // Keywords - Control flow
        Keyword_If, Keyword_Then, Keyword_Elsif, Keyword_Else, Keyword_EndIf,
        Keyword_Case, Keyword_EndCase,
        Keyword_For, Keyword_By, Keyword_Do, Keyword_EndFor,
        Keyword_While, Keyword_EndWhile,
        Keyword_Repeat, Keyword_Until, Keyword_EndRepeat,
        Keyword_Exit, Keyword_Continue, Keyword_Return,

        // Keywords - Operators (word operators)
        Keyword_And, Keyword_Or, Keyword_Xor, Keyword_Not,
        Keyword_AndThen, Keyword_OrElse,
        Keyword_Mod, Keyword_Expt,

        // Keywords - OOP
        Keyword_Extends, Keyword_Implements,
        Keyword_Method, Keyword_EndMethod,
        Keyword_Property, Keyword_EndProperty,
        Keyword_Interface, Keyword_EndInterface,
        Keyword_Abstract, Keyword_Final, Keyword_Override,
        Keyword_Public, Keyword_Private, Keyword_Protected, Keyword_Internal,
        Keyword_This, Keyword_Super,

        // Keywords - Configuration
        Keyword_Configuration, Keyword_EndConfiguration,
        Keyword_Resource, Keyword_EndResource,
        Keyword_Task, Keyword_With,
        Keyword_NonRetain,

        // Keywords - Other
        Keyword_Get, Keyword_Set, Keyword_New, Keyword_Delete, Keyword_RefTo,
        Keyword_Namespace, Keyword_EndNamespace,
        Keyword_Using,
        Keyword_Action, Keyword_EndAction,
        Keyword_Transition, Keyword_EndTransition,
        Keyword_True, Keyword_False,

        // Operators - Symbolic
        Plus,               // +
        Minus,              // -
        Star,               // *
        Slash,              // /
        StarStar,           // **
        Assign,             // :=
        OutputAssign,       // =>
        RefAssign,          // REF=
        Equal,              // =
        NotEqual,           // <>
        LessThan,           // <
        GreaterThan,        // >
        LessEqual,          // <=
        GreaterEqual,       // >=
        Caret,              // ^ (dereference)

        // Delimiters
        LeftParen,          // (
        RightParen,         // )
        LeftBracket,        // [
        RightBracket,       // ]
        LeftBrace,          // {
        RightBrace,         // }
        Semicolon,          // ;
        Comma,              // ,
        Dot,                // .
        DotDot,             // ..
        Hash,               // # (part of typed literals)

        // Pragma/Attribute
        Pragma,             // {anything inside braces} - entire pragma as one token
    }
}
