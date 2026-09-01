namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Represents a USING directive: USING namespace;
    /// </summary>
    public sealed class UsingDirective : SyntaxNode
    {
        public string NamespaceName { get; set; }
    }
}
