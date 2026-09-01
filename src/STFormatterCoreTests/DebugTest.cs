using STFormatterCore.Configuration;
using STFormatterCore.Formatter;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using System;

namespace STFormatterCoreTests
{
    public class DebugTest
    {
        public static void Main()
        {
            // Test 1: Indentation
            var source1 = "PROGRAM P\nIF x THEN\ny := 1;\nEND_IF\nEND_PROGRAM";
            var tokens1 = new STLexer(source1).Tokenize();
            var cst1 = new STParser(tokens1).Parse();
            var result1 = new STFormatter(new FormatterOptions()).Format(cst1, source1);
            Console.WriteLine("=== INDENTATION TEST ===");
            Console.WriteLine(result1.Replace("\r", "\\r").Replace("\n", "\\n\n"));
            Console.WriteLine("=== END ===");

            // Test 2: Trailing trivia
            var source2 = "x := 1; // end-of-line";
            var tokens2 = new STLexer(source2).Tokenize();
            Console.WriteLine("=== TRIVIA TEST ===");
            foreach (var tok in tokens2)
            {
                Console.WriteLine($"Token: [{tok.Kind}] '{tok.Text}' pos={tok.Position}");
                foreach (var lt in tok.LeadingTrivia)
                    Console.WriteLine($"  Leading: [{lt.Kind}] '{lt.Text}'");
                foreach (var tt in tok.TrailingTrivia)
                    Console.WriteLine($"  Trailing: [{tt.Kind}] '{tt.Text}'");
            }
            Console.WriteLine("=== END ===");

            // Test 3: Method parsing
            var source3 = "FUNCTION_BLOCK FB\nMETHOD PUBLIC DoStuff : BOOL\nEND_METHOD\nEND_FUNCTION_BLOCK";
            var tokens3 = new STLexer(source3).Tokenize();
            Console.WriteLine("=== METHOD TOKENS ===");
            foreach (var tok in tokens3)
            {
                Console.WriteLine($"Token: [{tok.Kind}] '{tok.Text}'");
            }
            var cst3 = new STParser(tokens3).Parse();
            Console.WriteLine("=== METHOD CST ===");
            var fb = cst3.Children[0] as STFormatterCore.Parser.Nodes.DeclarationBlock;
            Console.WriteLine($"FB children count: {fb.Children.Count}");
            foreach (var child in fb.Children)
            {
                Console.WriteLine($"  Child type: {child.GetType().Name}");
                if (child is STFormatterCore.Parser.Nodes.MethodDeclaration md)
                {
                    Console.WriteLine($"  Name: {md.Name}");
                    Console.WriteLine($"  AccessModifier: {md.AccessModifier}");
                    Console.WriteLine($"  ReturnType: {md.ReturnType}");
                    Console.WriteLine($"  Tokens:");
                    foreach (var t in md.Tokens)
                        Console.WriteLine($"    [{t.Kind}] '{t.Text}'");
                }
            }
            Console.WriteLine("=== END ===");
        }
    }
}
