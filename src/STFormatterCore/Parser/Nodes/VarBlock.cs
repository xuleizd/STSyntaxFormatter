namespace STFormatterCore.Parser.Nodes
{
    /// <summary>
    /// Represents a VAR...END_VAR block (any variant).
    /// </summary>
    public sealed class VarBlock : SyntaxNode
    {
        public VarKind VarKind { get; set; }
    }

    public enum VarKind
    {
        Var,
        VarInput,
        VarOutput,
        VarInOut,
        VarTemp,
        VarStat,
        VarInst,
        VarConstant,
        VarGlobal,
        VarExternal,
        VarAccess,
        VarConfig
    }
}
