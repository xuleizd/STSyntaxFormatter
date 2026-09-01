using System.Text;

namespace STFormatterCore.Formatter
{
    /// <summary>
    /// StringBuilder-based output accumulator for formatted text.
    /// </summary>
    public sealed class LineBuilder
    {
        private readonly StringBuilder _sb;
        private readonly string _lineEnding;
        private bool _atLineStart;
        private bool _indentWritten;
        private int _consecutiveNewlines;

        public LineBuilder(string lineEnding)
        {
            _sb = new StringBuilder();
            _lineEnding = lineEnding ?? "\n";
            _atLineStart = true;
            _indentWritten = false;
            _consecutiveNewlines = 0;
        }

        public bool IsAtLineStart => _atLineStart;

        /// <summary>
        /// True when nothing has been written yet. Used to avoid emitting a leading
        /// blank line before the first statement of a document/body.
        /// </summary>
        public bool IsEmpty => _sb.Length == 0;

        /// <summary>
        /// True when indent has been written but no content yet on this line.
        /// Used to suppress leading-trivia newlines that would create extra blank lines.
        /// </summary>
        public bool IsIndentWritten => _indentWritten;

        /// <summary>
        /// Writes the current indent if at the start of a line.
        /// </summary>
        public void WriteIndent(string indent)
        {
            bool wasAtLineStart = _atLineStart;
            if (_atLineStart && !string.IsNullOrEmpty(indent))
            {
                _sb.Append(indent);
            }
            _atLineStart = false;
            _indentWritten = true;
            // Don't reset _consecutiveNewlines when at line start.
            // This preserves blank line deduplication across indent boundaries
            // (e.g., blank lines before ELSE, END_IF, etc.).
            if (!wasAtLineStart)
                _consecutiveNewlines = 0;
        }

        /// <summary>
        /// Writes raw text (no indent auto-applied).
        /// </summary>
        public void Write(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _sb.Append(text);
            _atLineStart = false;
            _indentWritten = false;
            _consecutiveNewlines = 0;
        }

        /// <summary>
        /// Writes a keyword in uppercase.
        /// </summary>
        public void WriteKeyword(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return;
            _sb.Append(keyword.ToUpperInvariant());
            _atLineStart = false;
            _indentWritten = false;
            _consecutiveNewlines = 0;
        }

        /// <summary>
        /// Writes a newline and marks the next write as line-start.
        /// Deduplicates consecutive newlines to prevent excessive blank lines.
        /// </summary>
        public void WriteLine()
        {
            // Allow at most 2 consecutive newlines (one blank line)
            if (_consecutiveNewlines >= 2) return;
            _sb.Append(_lineEnding);
            _atLineStart = true;
            _indentWritten = false;
            _consecutiveNewlines++;
        }

        /// <summary>
        /// Writes a blank line (two newlines).
        /// </summary>
        public void WriteBlankLine()
        {
            if (_consecutiveNewlines >= 2) return;
            _sb.Append(_lineEnding);
            _sb.Append(_lineEnding);
            _atLineStart = true;
            _indentWritten = false;
            _consecutiveNewlines = 2;
        }

        /// <summary>
        /// Returns the built string.
        /// </summary>
        public string Build()
        {
            return _sb.ToString();
        }

        /// <summary>
        /// Length of the text written on the current line (since the last newline).
        /// Used for MaxLineLength-aware wrapping.
        /// </summary>
        public int CurrentLineLength
        {
            get
            {
                int start = _sb.Length - 1;
                while (start >= 0 && _sb[start] != '\n' && _sb[start] != '\r')
                    start--;
                return _sb.Length - 1 - start;
            }
        }
    }
}
