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
        private bool _codeOnLine;
        private bool _contentOnLine;
        private string _lastIndent;
        private int _consecutiveNewlines;

        public LineBuilder(string lineEnding)
        {
            _sb = new StringBuilder();
            _lineEnding = lineEnding ?? "\n";
            _atLineStart = true;
            _indentWritten = false;
            _codeOnLine = false;
            _contentOnLine = false;
            _lastIndent = string.Empty;
            _consecutiveNewlines = 0;
        }

        public bool IsAtLineStart => _atLineStart;

        /// <summary>
        /// True when the current line holds a real token, not just indentation or
        /// comments. <see cref="IsAtLineStart"/> can't answer this: WriteIndent
        /// clears it even when the indent is empty, so a line waiting for its first
        /// token looks like a line that already has code on it.
        /// </summary>
        public bool HasCodeOnLine => _codeOnLine;

        /// <summary>
        /// True when anything at all has been written on the current line, comments
        /// included. Decides whether a separating space is needed: a line holding
        /// only indentation must not start its first token with one.
        /// </summary>
        public bool HasContentOnLine => _contentOnLine;

        /// <summary>
        /// The indent given to the last <see cref="WriteIndent"/> call, so the next
        /// line can be placed at the same column.
        /// </summary>
        public string LastIndent => _lastIndent;

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
            _lastIndent = indent ?? string.Empty;
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
            _codeOnLine = true;
            _contentOnLine = true;
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
            _codeOnLine = true;
            _contentOnLine = true;
            _consecutiveNewlines = 0;
        }

        /// <summary>
        /// Writes comment text. Fills the line — a token following a block comment on
        /// the same line still needs its separating space — but does not count as
        /// code, so the next line is not mistaken for a continuation of an expression.
        /// </summary>
        public void WriteComment(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _sb.Append(text);
            _atLineStart = false;
            _indentWritten = false;
            _contentOnLine = true;
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
            _codeOnLine = false;
            _contentOnLine = false;
            _consecutiveNewlines++;
        }

        /// <summary>
        /// Ensures the output ends with exactly one blank line, adding only the
        /// line breaks that are still missing. Never stacks on top of a blank line
        /// that is already there, so a separator inserted by policy cannot grow on
        /// every formatting run.
        /// </summary>
        public void WriteBlankLine()
        {
            WriteBlankLines(1);
        }

        /// <summary>
        /// Ensures the output ends with at least <paramref name="count"/> blank
        /// lines. Used to carry a run of source blank lines through verbatim —
        /// KeepEmptyLines=true must not shrink what the user typed.
        /// </summary>
        public void WriteBlankLines(int count)
        {
            if (count < 1) count = 1;

            int wanted = count + 1; // newlines needed for `count` blank lines
            int missing = wanted - _consecutiveNewlines;
            if (missing <= 0) return;

            for (int i = 0; i < missing; i++)
                _sb.Append(_lineEnding);

            _atLineStart = true;
            _indentWritten = false;
            _codeOnLine = false;
            _contentOnLine = false;
            _consecutiveNewlines = wanted;
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
