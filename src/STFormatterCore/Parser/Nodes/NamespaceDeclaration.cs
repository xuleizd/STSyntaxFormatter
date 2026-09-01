namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Represents a NAMESPACE...END_NAMESPACE declaration.
    /// </summary>
    public sealed class NamespaceDeclaration : SyntaxNode
    {
        public string Name { get; set; }
    }
}
