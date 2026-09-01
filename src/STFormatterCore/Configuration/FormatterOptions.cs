namespace STFormatterCore.Configuration
{
    public class FormatterOptions
    {
        // Indentation
        public bool UseSpacesInsteadOfTab { get; set; } = true;
        public int IndentSize { get; set; } = 4;

        // Line
        public int MaxLineLength { get; set; } = 120;
        public LineEnding LineEnding { get; set; } = LineEnding.Auto;

        // Spacing
        public bool OperatorSpacing { get; set; } = true;      // a := b + c
        public bool CommaSpacing { get; set; } = true;          // Func(a, b, c)
        public bool ParenInnerSpacing { get; set; } = false;    // Func(a) not Func( a )

        // Alignment
        public bool AlignDeclarations { get; set; } = true;     // Align : in VAR blocks

        // Blank lines
        public int BlankLinesBeforeEnd { get; set; } = 0;
        public int BlankLinesAfterVar { get; set; } = 1;
        public bool KeepEmptyLines { get; set; } = true;

        // Type case
        public TypeCase TypeCase { get; set; } = TypeCase.Preserve;

        // Computed
        public string IndentString => UseSpacesInsteadOfTab
            ? new string(' ', IndentSize)
            : "\t";

        public string GetLineEnding(string source)
        {
            if (LineEnding == LineEnding.Auto)
                return source.Contains("\r\n") ? "\r\n" : "\n";
            return LineEnding == LineEnding.CRLF ? "\r\n" : "\n";
        }
    }

    public enum LineEnding { Auto, CRLF, LF }
    public enum TypeCase { Upper, Lower, Preserve }
}
