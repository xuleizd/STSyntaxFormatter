using CommandLine;

namespace STFormatterCLI
{
    public class Options
    {
        [Option('f', "file", HelpText = "Path to a .TcPOU, .TcDUT, or .TcGVL file to format")]
        public string File { get; set; }
        
        [Option('p', "project", HelpText = "Path to directory to recursively find .TcPOU/.TcDUT/.TcGVL files")]
        public string Project { get; set; }
        
        [Option("indentation", Default = 4, HelpText = "Number of spaces for indentation")]
        public int Indentation { get; set; }
        
        [Option("windowslineending", Default = false, HelpText = "Use Windows line endings (CRLF)")]
        public bool WindowsLineEnding { get; set; }
        
        [Option("unixlineending", Default = false, HelpText = "Use Unix line endings (LF)")]
        public bool UnixLineEnding { get; set; }
        
        [Option("lowercasekeywords", Default = false, HelpText = "Use lowercase keywords (NOT supported - keywords are always uppercase)")]
        public bool LowercaseKeywords { get; set; }
        
        [Option("safe", Default = false, HelpText = "Safe mode: verify formatted output matches input hash")]
        public bool Safe { get; set; }
        
        [Option('v', "verbose", Default = false, HelpText = "Verbose output")]
        public bool Verbose { get; set; }
        
        [Option("align-declarations", Default = true, HelpText = "Align colons in VAR blocks")]
        public bool AlignDeclarations { get; set; }
        
        [Option("keep-empty-lines", Default = true, HelpText = "Keep empty lines in code")]
        public bool KeepEmptyLines { get; set; }
        
        [Option("type-case", Default = "preserve", HelpText = "Type name case: upper, lower, or preserve")]
        public string TypeCase { get; set; }

        [Option("use-spaces", HelpText = "Use spaces instead of tabs (true/false, default: engine default)")]
        public bool? UseSpaces { get; set; }

        [Option("operator-spacing", HelpText = "Spaces around operators (true/false, default: engine default)")]
        public bool? OperatorSpacing { get; set; }

        [Option("comma-spacing", HelpText = "Space after commas (true/false, default: engine default)")]
        public bool? CommaSpacing { get; set; }

        [Option("paren-inner-spacing", HelpText = "Spaces inside parentheses (true/false, default: engine default)")]
        public bool? ParenInnerSpacing { get; set; }

        [Option("max-line-length", HelpText = "Wrap lines longer than this (default: engine default)")]
        public int? MaxLineLength { get; set; }

        [Option("blank-lines-after-var", HelpText = "Blank lines after VAR...END_VAR blocks (default: engine default)")]
        public int? BlankLinesAfterVar { get; set; }

        [Option("blank-lines-before-end", HelpText = "Blank lines before END_IF/END_FOR etc. (default: engine default)")]
        public int? BlankLinesBeforeEnd { get; set; }

        [Option("blank-lines-around-blocks", HelpText = "Blank line around outermost IF/CASE/FOR/WHILE/REPEAT blocks (true/false, default: engine default)")]
        public bool? BlankLinesAroundBlocks { get; set; }
    }
}
