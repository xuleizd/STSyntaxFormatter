using System;
using System.Collections.Generic;
using System.Linq;
using STFormatterCore.Configuration;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Parser.Nodes;

namespace STFormatterCore.Validation
{
    /// <summary>
    /// Outcome of an equivalence validation: formatting may only change
    /// whitespace, line breaks and the casing of keywords / type names — it
    /// must never add, drop, reorder or rewrite a token, and it must never
    /// lose or duplicate a comment.
    /// </summary>
    public sealed class FormattingValidationResult
    {
        public bool IsValid { get; }
        public string FailureMessage { get; }

        private FormattingValidationResult(bool isValid, string failureMessage)
        {
            IsValid = isValid;
            FailureMessage = failureMessage;
        }

        public static FormattingValidationResult Pass() =>
            new FormattingValidationResult(true, null);

        public static FormattingValidationResult Fail(string message) =>
            new FormattingValidationResult(false, message);
    }

    /// <summary>
    /// Machine safety net for the formatter (the counterpart of CSharpier's
    /// IFormattingValidator / SyntaxNodeComparer, adapted to ST). It re-lexes and
    /// re-parses both the source text and the formatted output, then compares:
    ///   1. the significant token sequences (kind + normalized text) — catches
    ///      tokens being dropped (STRUCT RETAIN, 1.8.7), reordered or invented
    ///      (the extra ';' of REPEAT, 1.8.6);
    ///   2. the CST node shape — catches whole constructs being regrouped even
    ///      when every token survives;
    ///   3. the multiset of comments — catches comments being deleted or
    ///      duplicated (the three 1.8.4 bugs).
    /// Callers must refuse to write the formatted text back when validation
    /// fails: a file left untouched is always better than a file corrupted.
    /// </summary>
    public static class FormattingValidator
    {
        /// <param name="options">Reserved for future normalization options; the
        /// current rules are option-independent (keyword casing and TypeCase are
        /// compared case-insensitively because the formatter is allowed to change
        /// them; string literals are compared case-sensitively because it is not).</param>
        public static FormattingValidationResult Validate(
            string source, string formatted, FormatterOptions options = null)
        {
            if (string.IsNullOrEmpty(formatted))
                return FormattingValidationResult.Fail("格式化输出为空——引擎故障，拒绝写回");

            CompilationUnit sourceTree;
            CompilationUnit outputTree;
            try
            {
                sourceTree = ParseText(source);
            }
            catch (Exception ex)
            {
                return FormattingValidationResult.Fail(
                    $"源代码无法解析，校验前提不成立：{ex.Message}");
            }
            try
            {
                outputTree = ParseText(formatted);
            }
            catch (Exception ex)
            {
                return FormattingValidationResult.Fail(
                    $"格式化输出无法重新解析（可能产出了残缺代码）：{ex.Message}");
            }

            var tokenFailure = CompareTokens(sourceTree, outputTree);
            if (tokenFailure != null) return FormattingValidationResult.Fail(tokenFailure);

            var shapeFailure = CompareShapes(sourceTree, outputTree);
            if (shapeFailure != null) return FormattingValidationResult.Fail(shapeFailure);

            // Comments are compared over the FULL token lists, not the trees:
            // on malformed input the parser can drop tokens that never enter any
            // node (the _Safety fixture), and comments attached to them would be
            // invisible to a tree walk exactly when they are most at risk.
            var commentFailure = CompareComments(
                new STLexer(source).Tokenize(), new STLexer(formatted).Tokenize());
            if (commentFailure != null) return FormattingValidationResult.Fail(commentFailure);

            return FormattingValidationResult.Pass();
        }

        private static CompilationUnit ParseText(string text)
        {
            var tokens = new STLexer(text).Tokenize();
            return new STParser(tokens).Parse();
        }

        #region Token sequence

        /// <summary>
        /// Collects the tokens that formatting must preserve, in source order.
        /// Two kinds of tokens are excluded on purpose:
        ///   - everything inside <see cref="UnknownNode"/>: the parser parks
        ///     unparsable debris (a stray ';' between statements, garbage around a
        ///     malformed VAR block) there, and normalizing such debris away is
        ///     established, intended formatter behaviour;
        ///   - non-header tokens of STRUCT/UNION bodies: members live in child
        ///     nodes, so any other token in the body's token list is the same kind
        ///     of debris (dropped today, deliberately).
        /// </summary>
        private static void CollectSignificantTokens(SyntaxNode node, List<Token> output)
        {
            if (node is UnknownNode) return;

            foreach (var token in node.Tokens)
            {
                if (token.Kind == TokenKind.EndOfFile) continue;
                if ((node is StructBody || node is UnionBody) && !IsBodyHeaderToken(token.Kind))
                    continue;
                output.Add(token);
            }

            foreach (var child in node.Children)
                CollectSignificantTokens(child, output);
        }

        private static bool IsBodyHeaderToken(TokenKind kind) =>
            kind == TokenKind.Keyword_Struct || kind == TokenKind.Keyword_Union ||
            kind == TokenKind.Keyword_Retain || kind == TokenKind.Keyword_Persistent ||
            kind == TokenKind.Keyword_EndStruct || kind == TokenKind.Keyword_EndUnion;

        private static string CompareTokens(CompilationUnit sourceTree, CompilationUnit outputTree)
        {
            var sourceTokens = new List<Token>();
            var outputTokens = new List<Token>();
            CollectSignificantTokens(sourceTree, sourceTokens);
            CollectSignificantTokens(outputTree, outputTokens);

            if (sourceTokens.Count != outputTokens.Count)
            {
                var detail = FirstTokenDifference(sourceTokens, outputTokens);
                return $"格式化改变了有效 token 数量（源 {sourceTokens.Count} 个 → 输出 {outputTokens.Count} 个）。" +
                       $"首个差异：{detail}";
            }

            for (int i = 0; i < sourceTokens.Count; i++)
            {
                if (!TokenEquals(sourceTokens[i], outputTokens[i]))
                {
                    return $"第 {i + 1} 个有效 token 不一致：源 '{sourceTokens[i].Text}' ({sourceTokens[i].Kind})" +
                           $" vs 输出 '{outputTokens[i].Text}' ({outputTokens[i].Kind})";
                }
            }

            return null;
        }

        private static string FirstTokenDifference(List<Token> sourceTokens, List<Token> outputTokens)
        {
            int common = Math.Min(sourceTokens.Count, outputTokens.Count);
            for (int i = 0; i < common; i++)
            {
                if (!TokenEquals(sourceTokens[i], outputTokens[i]))
                {
                    return $"位置 {i + 1}：源 '{sourceTokens[i].Text}' ({sourceTokens[i].Kind})" +
                           $" vs 输出 '{outputTokens[i].Text}' ({outputTokens[i].Kind})";
                }
            }
            var longer = sourceTokens.Count > outputTokens.Count ? sourceTokens : outputTokens;
            var side = sourceTokens.Count > outputTokens.Count ? "源" : "输出";
            if (common < longer.Count)
                return $"位置 {common + 1} 起多出（{side}）：'{longer[common].Text}' ({longer[common].Kind})";
            return "（数量不同但公共前缀一致）";
        }

        /// <summary>
        /// Keyword casing and TypeCase are legitimate formatting changes, so
        /// identifiers and keywords compare case-insensitively — the validator
        /// cannot tell which identifier is a type name. Literals (strings above
        /// all) must survive verbatim, so they compare case-sensitively.
        /// </summary>
        private static bool TokenEquals(Token a, Token b)
        {
            if (a.Kind != b.Kind) return false;
            if (IsCaseSensitiveToken(a.Kind))
                return string.Equals(a.Text, b.Text, StringComparison.Ordinal);
            return string.Equals(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCaseSensitiveToken(TokenKind kind) =>
            kind == TokenKind.StringLiteral ||
            kind == TokenKind.WStringLiteral ||
            kind == TokenKind.TypedLiteral ||
            kind == TokenKind.DirectAddress ||
            kind == TokenKind.IntegerLiteral ||
            kind == TokenKind.RealLiteral;

        #endregion

        #region CST shape

        /// <summary>
        /// Compares the tree shape of both trees (node type + nesting depth,
        /// children order, <see cref="UnknownNode"/> skipped). The depth is part
        /// of the shape on purpose: a statement relocated from outside a loop
        /// into its body keeps the same flat node sequence but changes depth —
        /// a corruption the token-sequence comparison alone cannot see.
        /// </summary>
        private static string CompareShapes(CompilationUnit sourceTree, CompilationUnit outputTree)
        {
            var sourceShape = new List<string>();
            var outputShape = new List<string>();
            CollectShape(sourceTree, sourceShape, 0);
            CollectShape(outputTree, outputShape, 0);

            if (sourceShape.Count == outputShape.Count)
            {
                for (int i = 0; i < sourceShape.Count; i++)
                {
                    if (sourceShape[i] != outputShape[i])
                        return $"语法树结构不一致：位置 {i + 1} 源为 {sourceShape[i]}，输出为 {outputShape[i]}";
                }
                return null;
            }

            int common = 0;
            while (common < Math.Min(sourceShape.Count, outputShape.Count) &&
                   sourceShape[common] == outputShape[common])
                common++;
            var side = sourceShape.Count > outputShape.Count ? "源" : "输出";
            var longer = sourceShape.Count > outputShape.Count ? sourceShape : outputShape;
            return $"语法树节点数量不一致（源 {sourceShape.Count} 个 → 输出 {outputShape.Count} 个），" +
                   $"位置 {common + 1} 起 {side}多出 {longer[Math.Min(common, longer.Count - 1)]}";
        }

        private static void CollectShape(SyntaxNode node, List<string> output, int depth)
        {
            if (node is UnknownNode) return;
            output.Add(new string('·', depth) + node.GetType().Name);
            foreach (var child in node.Children)
                CollectShape(child, output, depth + 1);
        }

        #endregion

        #region Comments

        /// <summary>
        /// Compares the multiset of comments over the full token lists. Comment
        /// text is normalized by stripping all whitespace (the formatter adjusts
        /// the gap after '//' and before '(*' — that is intended — but must never
        /// touch the content, lose or duplicate a comment).
        /// </summary>
        private static string CompareComments(System.Collections.Generic.List<Token> sourceTokens,
                                              System.Collections.Generic.List<Token> outputTokens)
        {
            var sourceComments = CollectComments(sourceTokens);
            var outputComments = CollectComments(outputTokens);

            var missing = MultisetDifference(sourceComments, outputComments);
            if (missing.Count > 0)
                return $"格式化丢失了 {missing.Count} 条注释：{Describe(missing)}";

            var added = MultisetDifference(outputComments, sourceComments);
            if (added.Count > 0)
                return $"格式化凭空多出 {added.Count} 条注释：{Describe(added)}";

            return null;
        }

        private static List<string> CollectComments(System.Collections.Generic.List<Token> tokens)
        {
            var comments = new List<string>();
            foreach (var token in tokens)
            {
                AddTriviaComments(token.LeadingTrivia, comments);
                AddTriviaComments(token.TrailingTrivia, comments);
            }
            return comments;
        }

        private static void AddTriviaComments(List<Trivia> trivia, List<string> output)
        {
            if (trivia == null) return;
            foreach (var item in trivia)
            {
                if (item.Kind == TriviaKind.SingleLineComment ||
                    item.Kind == TriviaKind.MultiLineComment)
                {
                    output.Add(NormalizeComment(item.Text));
                }
            }
        }

        private static string NormalizeComment(string text)
        {
            var chars = text.Where(c => !char.IsWhiteSpace(c)).ToArray();
            return new string(chars);
        }

        private static List<string> MultisetDifference(List<string> from, List<string> against)
        {
            var remaining = new Dictionary<string, int>();
            foreach (var item in against)
                remaining[item] = remaining.TryGetValue(item, out var n) ? n + 1 : 1;

            var difference = new List<string>();
            foreach (var item in from)
            {
                if (remaining.TryGetValue(item, out var n) && n > 0)
                {
                    remaining[item] = n - 1;
                }
                else
                {
                    difference.Add(item);
                }
            }
            return difference;
        }

        private static string Describe(List<string> items) =>
            string.Join(", ", items.Select(c => $"'{(c.Length > 40 ? c.Substring(0, 40) + "…" : c)}'"));

        #endregion
    }
}
