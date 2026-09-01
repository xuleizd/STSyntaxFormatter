using System.Collections.Generic;
using STFormatterCore.Lexer;

namespace STFormatterCore.Parser
{
    /// <summary>
    /// Base class for all Concrete Syntax Tree (CST) nodes.
    /// Each node owns its direct tokens and child nodes.
    /// </summary>
    public abstract class SyntaxNode
    {
        public List<SyntaxNode> Children { get; } = new List<SyntaxNode>();
        public List<Token> Tokens { get; } = new List<Token>();

        public void AddChild(SyntaxNode child) => Children.Add(child);
        public void AddToken(Token token) => Tokens.Add(token);
    }
}
