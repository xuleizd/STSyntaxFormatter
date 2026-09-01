namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Represents an ENUM body (value1, value2 := n, ...) with optional base type.
    /// </summary>
    public sealed class EnumBody : SyntaxNode
    {
        public string BaseType { get; set; }  // Optional base type like INT
    }
}
