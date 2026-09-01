namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Represents a POU declaration: PROGRAM, FUNCTION, FUNCTION_BLOCK, or INTERFACE.
    /// </summary>
    public sealed class DeclarationBlock : SyntaxNode
    {
        public DeclarationKind DeclarationKind { get; set; }
        public string Name { get; set; }

        // EXTENDS/IMPLEMENTS clause tokens (if any)
        public string ExtendsName { get; set; }
        public System.Collections.Generic.List<string> ImplementsNames { get; set; }
            = new System.Collections.Generic.List<string>();
    }

    public enum DeclarationKind
    {
        Program,
        Function,
        FunctionBlock,
        Interface
    }
}
