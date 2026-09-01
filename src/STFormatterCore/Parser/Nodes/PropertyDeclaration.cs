namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Represents a PROPERTY...END_PROPERTY declaration with optional GET/SET blocks.
    /// </summary>
    public sealed class PropertyDeclaration : SyntaxNode
    {
        public string Name { get; set; }
        public string AccessModifier { get; set; }
        public string ReturnType { get; set; }
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
    }
}
