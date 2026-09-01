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
    }
}
