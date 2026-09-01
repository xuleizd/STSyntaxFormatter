namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Represents a METHOD...END_METHOD declaration.
    /// </summary>
    public sealed class MethodDeclaration : SyntaxNode
    {
        public string Name { get; set; }
        public string AccessModifier { get; set; }  // PUBLIC, PRIVATE, PROTECTED, INTERNAL
        public string ReturnType { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsFinal { get; set; }
        public bool IsOverride { get; set; }
    }
}
