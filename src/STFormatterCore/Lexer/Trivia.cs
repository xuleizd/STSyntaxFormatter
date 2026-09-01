namespace STFormatterCore.Lexer
{
    public sealed class Trivia
    {
        public TriviaKind Kind { get; }
        public string Text { get; }

        public Trivia(TriviaKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }
}
