using System.ComponentModel;
using Microsoft.VisualStudio.Shell;
using STFormatterCore.Configuration;

namespace STFormatterVSIX
{
    /// <summary>
    /// Options page shown under Tools → Options → ST Formatter.
    /// </summary>
    public class OptionsPage : DialogPage
    {
        // Indentation
        private bool useSpacesInsteadOfTab = true;
        private int indentSize = 4;

        // Spacing
        private bool operatorSpacing = true;
        private bool commaSpacing = true;
        private bool parenInnerSpacing = false;

        // Alignment
        private bool alignDeclarations = true;

        // Line
        private int maxLineLength = 120;
        private LineEnding lineEnding = LineEnding.Auto;

        // Blank lines
        private bool keepEmptyLines = true;
        private int blankLinesAfterVar = 1;
        private int blankLinesBeforeEnd = 0;

        // Type case
        private TypeCase typeCase = TypeCase.Preserve;

        // Behavior
        private bool formatOnSave = true;

        [Category("Indentation")]
        [DisplayName("Use Spaces")]
        [Description("Use spaces instead of tabs for indentation.")]
        public bool UseSpacesInsteadOfTab
        {
            get { return useSpacesInsteadOfTab; }
            set { useSpacesInsteadOfTab = value; }
        }

        [Category("Indentation")]
        [DisplayName("Indent Size")]
        [Description("Number of spaces per indent level.")]
        public int IndentSize
        {
            get { return indentSize; }
            set { indentSize = value; }
        }

        [Category("Formatting")]
        [DisplayName("Operator Spacing")]
        [Description("Add spaces around operators (e.g. a := b + c).")]
        public bool OperatorSpacing
        {
            get { return operatorSpacing; }
            set { operatorSpacing = value; }
        }

        [Category("Formatting")]
        [DisplayName("Comma Spacing")]
        [Description("Add space after commas (e.g. Func(a, b, c)).")]
        public bool CommaSpacing
        {
            get { return commaSpacing; }
            set { commaSpacing = value; }
        }

        [Category("Formatting")]
        [DisplayName("Align Declarations")]
        [Description("Align colons in VAR blocks.")]
        public bool AlignDeclarations
        {
            get { return alignDeclarations; }
            set { alignDeclarations = value; }
        }

        [Category("Formatting")]
        [DisplayName("Keep Empty Lines")]
        [Description("Preserve existing empty lines during formatting.")]
        public bool KeepEmptyLines
        {
            get { return keepEmptyLines; }
            set { keepEmptyLines = value; }
        }

        [Category("Formatting")]
        [DisplayName("Blank Lines After VAR")]
        [Description("Number of blank lines to insert after VAR...END_VAR blocks.")]
        public int BlankLinesAfterVar
        {
            get { return blankLinesAfterVar; }
            set { blankLinesAfterVar = value; }
        }

        [Category("Formatting")]
        [DisplayName("Max Line Length")]
        [Description("Maximum line length before wrapping.")]
        public int MaxLineLength
        {
            get { return maxLineLength; }
            set { maxLineLength = value; }
        }

        [Category("Formatting")]
        [DisplayName("Line Ending")]
        [Description("Line ending style: Auto, CRLF, or LF.")]
        public LineEnding LineEnding
        {
            get { return lineEnding; }
            set { lineEnding = value; }
        }

        [Category("Formatting")]
        [DisplayName("Parenthesis Inner Spacing")]
        [Description("Add spaces inside parentheses (e.g. ( a ) vs (a)).")]
        public bool ParenInnerSpacing
        {
            get { return parenInnerSpacing; }
            set { parenInnerSpacing = value; }
        }

        [Category("Formatting")]
        [DisplayName("Blank Lines Before End")]
        [Description("Number of blank lines to insert before END_IF, END_FOR, etc.")]
        public int BlankLinesBeforeEnd
        {
            get { return blankLinesBeforeEnd; }
            set { blankLinesBeforeEnd = value; }
        }

        [Category("Formatting")]
        [DisplayName("Type Case")]
        [Description("How to format type names: Upper, Lower, or Preserve.")]
        public TypeCase TypeCase
        {
            get { return typeCase; }
            set { typeCase = value; }
        }

        [Category("Behavior")]
        [DisplayName("Format On Save")]
        [Description("Automatically format .TcPOU files when saved.")]
        public bool FormatOnSave
        {
            get { return formatOnSave; }
            set { formatOnSave = value; }
        }

        /// <summary>
        /// Converts the options page settings to a FormatterOptions instance.
        /// </summary>
        public FormatterOptions ToFormatterOptions()
        {
            return new FormatterOptions
            {
                UseSpacesInsteadOfTab = UseSpacesInsteadOfTab,
                IndentSize = IndentSize,
                OperatorSpacing = OperatorSpacing,
                CommaSpacing = CommaSpacing,
                AlignDeclarations = AlignDeclarations,
                KeepEmptyLines = KeepEmptyLines,
                BlankLinesAfterVar = BlankLinesAfterVar,
                BlankLinesBeforeEnd = BlankLinesBeforeEnd,
                MaxLineLength = MaxLineLength,
                LineEnding = LineEnding,
                ParenInnerSpacing = ParenInnerSpacing,
                TypeCase = TypeCase,
            };
        }
    }
}
