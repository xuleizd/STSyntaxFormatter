using System.Collections.Generic;

namespace STFormatterCore.Lexer
{
    public sealed class Token
    {
        public TokenKind Kind { get; }
        public string Text { get; }
        public int Position { get; }
        public List<Trivia> LeadingTrivia { get; }
        public List<Trivia> TrailingTrivia { get; }

        public Token(TokenKind kind, string text, int position,
                     List<Trivia> leadingTrivia = null,
                     List<Trivia> trailingTrivia = null)
        {
            Kind = kind;
            Text = text;
            Position = position;
            LeadingTrivia = leadingTrivia ?? new List<Trivia>();
            TrailingTrivia = trailingTrivia ?? new List<Trivia>();
        }
    }
}
