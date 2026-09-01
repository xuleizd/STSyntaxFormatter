using System;

namespace STFormatterCore.Formatter
{
    /// <summary>
    /// Tracks indentation level and provides the current indentation string.
    /// </summary>
    public sealed class IndentationManager
    {
        private readonly string _indentUnit;
        private int _level;

        public IndentationManager(string indentUnit)
        {
            _indentUnit = indentUnit ?? "    ";
            _level = 0;
        }

        public int Level => _level;

        public string CurrentIndent
        {
            get
            {
                if (_level <= 0) return string.Empty;
                var sb = new System.Text.StringBuilder(_indentUnit.Length * _level);
                for (int i = 0; i < _level; i++)
                    sb.Append(_indentUnit);
                return sb.ToString();
            }
        }

        public void Increase() => _level++;

        public void Decrease()
        {
            if (_level > 0) _level--;
        }

        /// <summary>
        /// Returns an IDisposable that increases indent on creation and decreases on dispose.
        /// Usage: using (indent.Push()) { ... }
        /// </summary>
        public IDisposable Push()
        {
            Increase();
            return new IndentScope(this);
        }

        private sealed class IndentScope : IDisposable
        {
            private readonly IndentationManager _manager;

            public IndentScope(IndentationManager manager)
            {
                _manager = manager;
            }

            public void Dispose()
            {
                _manager.Decrease();
            }
        }
    }
}
