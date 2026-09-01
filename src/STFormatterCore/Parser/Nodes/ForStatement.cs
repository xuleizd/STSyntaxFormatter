namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Represents a FOR...END_FOR loop statement.
    /// </summary>
    public sealed class ForStatement : SyntaxNode
    {
        // Tokens include: FOR keyword, counter variable, :=, start expression,
        // TO keyword, end expression, optional BY step, DO keyword
    }
}
