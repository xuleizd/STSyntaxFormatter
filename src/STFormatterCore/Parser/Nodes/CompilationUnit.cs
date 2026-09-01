namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Root node of the CST. Contains all top-level declarations.
    /// Children: DeclarationBlock, NamespaceDeclaration, UsingDirective, AttributeDirective, UnknownNode
    /// </summary>
    public sealed class CompilationUnit : SyntaxNode
    {
    }
}
