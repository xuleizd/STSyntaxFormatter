namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Represents a TYPE...END_TYPE declaration wrapping STRUCT/ENUM/UNION.
    /// </summary>
    public sealed class TypeDeclaration : SyntaxNode
    {
        public string TypeName { get; set; }
    }
}
