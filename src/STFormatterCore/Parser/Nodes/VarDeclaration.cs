namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Represents a single variable declaration inside a VAR block.
    /// Example: myVar : INT := 0;
    /// </summary>
    public sealed class VarDeclaration : SyntaxNode
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public bool HasInitialization { get; set; }
        public string Address { get; set; }  // AT address if present
    }
}
