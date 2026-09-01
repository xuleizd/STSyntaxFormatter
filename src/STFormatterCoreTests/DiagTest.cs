using System.Linq;
using STFormatterCore.Configuration;
using STFormatterCore.Formatter;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using Xunit;
using Xunit.Abstractions;

namespace STFormatterCoreTests
{
    public class DiagFormatterTest
    {
        private readonly ITestOutputHelper _output;
        public DiagFormatterTest(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void DiagWrappedCode()
        {
            var implCode = "IF NOT enable THEN\n\terror := FALSE;\nEND_IF\n\nrTrig(CLK:= enable, Q=> );\n\nCASE writeState OF\n\t0:\n\t\tIF rTrig.Q THEN\n\t\t\twriteState := 1;\n\t\tEND_IF\nEND_CASE";

            // Wrap in PROGRAM
            var wrapped = "PROGRAM __Temp__\n" + implCode + "\nEND_PROGRAM";

            _output.WriteLine("=== WRAPPED INPUT ===");
            _output.WriteLine(wrapped);

            var options = new FormatterOptions();
            var tokens = new STLexer(wrapped).Tokenize();

            _output.WriteLine("\n=== TOKENS (first 30) ===");
            int count = 0;
            foreach (var tok in tokens)
            {
                if (count++ >= 30) break;
                _output.WriteLine("  [{0}] '{1}'", tok.Kind, tok.Text.Replace("\n","\\n").Replace("\r","\\r"));
            }

            var cst = new STParser(tokens).Parse();

            _output.WriteLine("\n=== CST ===");
            _output.WriteLine("Root children: {0}", cst.Children.Count);
            foreach (var child in cst.Children)
            {
                _output.WriteLine("  Node: {0}, Tokens: {1}, Children: {2}", child.GetType().Name, child.Tokens.Count, child.Children.Count);
                foreach (var c in child.Children)
                {
                    _output.WriteLine("    Child: {0}, Tokens: {1}", c.GetType().Name, c.Tokens.Count);
                }
            }

            var result = new STFormatter(options).Format(cst, wrapped);

            _output.WriteLine("\n=== OUTPUT ===");
            _output.WriteLine(result);
        }

        [Fact]
        public void DiagBareCode()
        {
            var source = "IF NOT enable THEN\n\terror := FALSE;\nEND_IF";

            _output.WriteLine("=== BARE INPUT ===");
            _output.WriteLine(source);

            var options = new FormatterOptions();
            var tokens = new STLexer(source).Tokenize();
            var cst = new STParser(tokens).Parse();

            _output.WriteLine("\n=== CST ===");
            _output.WriteLine("Root children: {0}", cst.Children.Count);
            foreach (var child in cst.Children)
            {
                _output.WriteLine("  Node: {0}, Tokens: {1}, Children: {2}", child.GetType().Name, child.Tokens.Count, child.Children.Count);
            }

            var result = new STFormatter(options).Format(cst, source);

            _output.WriteLine("\n=== OUTPUT ===");
            _output.WriteLine(result);
        }

        [Fact]
        public void DiagCaseWithComments()
        {
            var implCode = "CASE iStep OF\n    0:\n        ;\n    // comment before 10\n    10:\n        x := 1;\n    // comment before 20\n    20:\n        IF b THEN\n            y := 2;\n        END_IF\n    30,999:\n        z := 3;\nEND_CASE";
            var wrapped = "PROGRAM __Temp__\n" + implCode + "\nEND_PROGRAM";

            _output.WriteLine("=== INPUT ===");
            _output.WriteLine(wrapped);

            var options = new FormatterOptions();
            var tokens = new STLexer(wrapped).Tokenize();

            _output.WriteLine("\n=== ALL TOKENS ===");
            for (int i = 0; i < tokens.Count; i++)
            {
                var tok = tokens[i];
                var triviaStr = "";
                if (tok.LeadingTrivia != null)
                    foreach (var t in tok.LeadingTrivia)
                        triviaStr += $" [L:{t.Kind}='{t.Text.Replace("\n","\\n").Replace("\r","\\r")}']";
                _output.WriteLine("  [{0}] chars=[{1}] display='{2}'{3}", tok.Kind, string.Join(",", tok.Text.Select(c => "U+" + ((int)c).ToString("X4"))), tok.Text.Replace("\n","\\n").Replace("\r","\\r"), triviaStr);
            }

            var cst = new STParser(tokens).Parse();

            _output.WriteLine("\n=== CST ===");
            PrintNode(cst, 0);

            var result = new STFormatter(options).Format(cst, wrapped);
            _output.WriteLine("\n=== OUTPUT ===");
            _output.WriteLine(result);
        }

        [Fact]
        public void DiagCaseNestedIf()
        {
            var implCode = "CASE iStep OF\n" +
                "    0:\n" +
                "        ;\n" +
                "    20:\n" +
                "        IF fbCreate(x := 1) THEN\n" +
                "            ipResult := fb.ipResult;\n" +
                "        // comment\n" +
                "            IF fb.bError\n" +
                "            OR nID = 0 THEN\n" +
                "                iStep := 999;\n" +
                "            ELSE\n" +
                "                iStep := iStep + 10;\n" +
                "            END_IF\n" +
                "        END_IF\n" +
                "    30,999:\n" +
                "        udiErrorID := 0;\n" +
                "END_CASE";
            var wrapped = "PROGRAM __Temp__\n" + implCode + "\nEND_PROGRAM";

            var options = new FormatterOptions();
            var tokens = new STLexer(wrapped).Tokenize();
            var cst = new STParser(tokens).Parse();

            _output.WriteLine("=== CST ===");
            PrintNode(cst, 0);

            var result = new STFormatter(options).Format(cst, wrapped);
            _output.WriteLine("\n=== OUTPUT ===");
            _output.WriteLine(result);
        }

        [Fact]
        public void DiagVarBlockWithDottedTypes()
        {
            // Minimal test: just VAR with WSTRING(255) followed by another var
            var source = "FUNCTION_BLOCK FB_Test\n" +
                "VAR\n" +
                "    wsErrorText : WSTRING(255);\n" +
                "    nextVar : INT;\n" +
                "END_VAR\n" +
                "END_FUNCTION_BLOCK";

            var options = new FormatterOptions();
            var tokens = new STLexer(source).Tokenize();

            _output.WriteLine("=== TOKENS around VAR ===");
            for (int i = 0; i < tokens.Count; i++)
            {
                var tok = tokens[i];
                if (tok.Text == "VAR" || tok.Text == "END_VAR" || tok.Kind == TokenKind.Keyword_Var || tok.Kind == TokenKind.Keyword_EndVar)
                    _output.WriteLine("  [{0}] Kind={1} Text='{2}'", i, tok.Kind, tok.Text);
            }

            var cst = new STParser(tokens).Parse();

            _output.WriteLine("\n=== CST ===");
            PrintNode(cst, 0);

            var result = new STFormatter(options).Format(cst, source);
            _output.WriteLine("\n=== OUTPUT ===");
            _output.WriteLine(result);
        }

        [Fact]
        public void DiagNestedIfElse()
        {
            // Simple nested IF/ELSE test
            var implCode = "IF a THEN\n" +
                "    x := 1;\n" +
                "    IF b THEN\n" +
                "        y := 2;\n" +
                "    ELSE\n" +
                "        z := 3;\n" +
                "    END_IF\n" +
                "ELSE\n" +
                "    w := 4;\n" +
                "END_IF\n";
            var wrapped = "PROGRAM __Temp__\n" + implCode + "\nEND_PROGRAM";

            var options = new FormatterOptions();
            var tokens = new STLexer(wrapped).Tokenize();
            var cst = new STParser(tokens).Parse();

            _output.WriteLine("=== CST ===");
            PrintNode(cst, 0);

            var result = new STFormatter(options).Format(cst, wrapped);
            _output.WriteLine("\n=== OUTPUT ===");
            _output.WriteLine(result);
        }

        [Fact]
        public void DiagFullFile()
        {
            // Declaration section
            var declCode = "FUNCTION_BLOCK FB_ConnSQLServer\n" +
                "    VAR_INPUT\n" +
                "        bExecute  : BOOL;\n" +
                "        sServerIP : STRING := '192.168.77.250';\n" +
                "        sDBName   : STRING := 'n23052';\n" +
                "        sUserName : STRING := 'xulei';\n" +
                "        sPassword : STRING := 'twtddd1102';\n" +
                "    END_VAR\n" +
                "\n" +
                "    VAR_OUTPUT\n" +
                "        udiDBID     : UDINT;\n" +
                "        bBusy       : BOOL;\n" +
                "        bDone       : BOOL;\n" +
                "        bError      : BOOL;\n" +
                "        udiErrorID  : UDINT;\n" +
                "        wsErrorText : WSTRING(255);\n" +
                "    END_VAR\n" +
                "\n" +
                "    VAR\n" +
                "        fbTrig_Exe : Tc2_Standard.R_TRIG;\n" +
                "        iStep : INT;\n" +
                "        _udiDBID : UDINT;\n" +
                "        ipResultEvt : Tc3_EventLogger.I_TcResultEvent;\n" +
                "        stDBConfig : FB_ConfigTcDBSrv(sNetID:='', tTimeout:=T#10S);\n" +
                "        fbPLCDBConnect : FB_ConnSQLServer;\n" +
                "    END_VAR\n" +
                "\n" +
                "END_FUNCTION_BLOCK\n";

            var options = new FormatterOptions();
            var tokens = new STLexer(declCode).Tokenize();
            var cst = new STParser(tokens).Parse();

            _output.WriteLine("=== DECLARATION CST ===");
            PrintNode(cst, 0);

            var result = new STFormatter(options).Format(cst, declCode);
            _output.WriteLine("\n=== DECLARATION OUTPUT ===");
            _output.WriteLine(result);

            // Implementation section (wrapped)
            var implCode = "    // 连接到数据库\n\n" +
                "fbTrig_Exe(CLK := bExecute);\n" +
                "    IF fbTrig_Exe.Q AND NOT bBusy THEN\n" +
                "        iStep := 10;\n" +
                "    END_IF\n" +
                "    CASE iStep OF\n" +
                "        0:\n" +
                "            ;\n" +
                "        // 数据库参数配置\n" +
                "        10:\n" +
                "            _udiDBID := 0;\n" +
                "            stDBConfig.sServer := sServerIP;\n" +
                "            stDBConfig.nPort := 3309;\n" +
                "            stDBConfig.sDatabase := sDBName;\n" +
                "            stDBConfig.bAuthentification := TRUE;\n" +
                "            stDBConfig.sUserID := sUserName;\n" +
                "            stDBConfig.sPassword := sPassword;\n" +
                "            iStep := iStep + 10;\n" +
                "        // 数据库连接配置\n" +
                "        20:\n" +
                "            IF fbPLCDBConnect.Create(pTcDBSrvConfig := ADR(stDBConfig), cbTcDBSrvConfig := SIZEOF(stDBConfig), bTemporary := TRUE, pConfigID := ADR(_udiDBID)) THEN\n" +
                "                ipResultEvt := fbPLCDBConnect.ipTcResultEvent;\n" +
                "                // 数据库连接结果反馈\n" +
                "\n" +
                "\n" +
                "                IF fbPLCDBConnect.bError OR _udiDBID = 0 OR (ipResultEvt.Severity = E_Severity.Error) OR (ipResultEvt.Severity = E_Severity.Critical) THEN\n" +
                "                    iStep := 999;\n" +
                "                    ELSE\n" +
                "                        udiDBID := _udiDBID;\n" +
                "                        iStep := iStep + 10;\n" +
                "                END_IF\n" +
                "            END_IF\n" +
                "        30,999:\n" +
                "            //\t\tEventClassName := ipResultEvt.EventClassDisplayName;\n" +
                "\n" +
                "//\t\tEventSourcePath := ipResultEvt.SourcePath;\n" +
                "  udiErrorID := ipResultEvt.EventId;\n" +
                "            wsErrorText := ipResultEvt.Text;\n" +
                "            IF NOT bExecute THEN\n" +
                "                iStep := 0;\n" +
                "            END_IF\n" +
                "    END_CASE\n" +
                "    bBusy := iStep >= 10 AND iStep < 30;\n" +
                "    bDone := iStep = 30;\n" +
                "    bError := iStep = 999;\n";

            var wrapped = "PROGRAM __Temp__\n" + implCode + "\nEND_PROGRAM";
            var tokens2 = new STLexer(wrapped).Tokenize();
            var cst2 = new STParser(tokens2).Parse();
            var result2 = new STFormatter(options).Format(cst2, wrapped);

            _output.WriteLine("\n=== IMPLEMENTATION OUTPUT ===");
            _output.WriteLine(result2);
        }

        private void PrintNode(STFormatterCore.Parser.SyntaxNode node, int depth)
        {
            var prefix = new string(' ', depth * 2);
            _output.WriteLine("{0}{1} Tokens={2} Children={3}",
                prefix, node.GetType().Name, node.Tokens.Count, node.Children.Count);
            foreach (var tok in node.Tokens)
            {
                _output.WriteLine("{0}  T: '{1}' ({2})", prefix, tok.Text.Replace("\n","\\n"), tok.Kind);
            }
            foreach (var child in node.Children)
            {
                PrintNode(child, depth + 1);
            }
        }
    }
}
