namespace STFormatterCore.Lexer
{
    public enum TriviaKind
    {
        Whitespace,         // spaces, tabs (not newlines)
        NewLine,            // \r\n or \n
        SingleLineComment,  // // ...
        MultiLineComment,   // (* ... *) or /* ... */
    }
}
