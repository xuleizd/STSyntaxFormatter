using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using STFormatterCore.Lexer;
using STFormatterCore.Parser;
using STFormatterCore.Formatter;
using STFormatterCore.Configuration;

namespace STFormatterCLI
{
    public class TcPouFile
    {
        private readonly string _path;
        private XmlDocument _doc;
        private string _lineEnding;
        private bool _isOldFormat;
        
        /// <summary>
        /// Supported TwinCAT3 XML file extensions.
        /// </summary>
        public static readonly string[] SupportedExtensions = { ".TcPOU", ".TcDUT", ".TcGVL" };
        
        /// <summary>
        /// Checks whether the given file path has a supported extension.
        /// </summary>
        public static bool IsSupportedFile(string path)
        {
            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;
            foreach (var supported in SupportedExtensions)
                if (ext.Equals(supported, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        
        public TcPouFile(string path)
        {
            _path = path;
            _doc = new XmlDocument();
            _doc.Load(path);
            
            // Auto-detect line ending
            string text = _doc.InnerXml;
            _lineEnding = text.Contains("\r\n") ? "\r\n" : "\n";
            
            // Detect old XML format (Single/Array/TextLines structure)
            _isOldFormat = _doc.SelectSingleNode("//Single[@Name='Object']/Single[@Name='Implementation']/Single[@Name='TextDocument']") != null
                        || _doc.SelectSingleNode("//Array[@Name='TextLines']") != null;
        }
        
        public void Format(FormatterOptions options, bool validate = true)
        {
            // Override line ending if configured
            if (options.LineEnding == LineEnding.CRLF) _lineEnding = "\r\n";
            else if (options.LineEnding == LineEnding.LF) _lineEnding = "\n";

            if (_isOldFormat)
                FormatOldFormat(options, validate);
            else
                FormatNewFormat(options, validate);
        }
        
        /// <summary>
        /// Format new-style TcPlcObject files with Declaration and Implementation/ST CDATA sections.
        /// </summary>
        private void FormatNewFormat(FormatterOptions options, bool validate)
        {
            // Format all Declaration nodes
            FormatNodes(".//Declaration", options, validate);
            // Format all Implementation/ST nodes (bare ST code - needs wrapping)
            FormatNodesWrapped(".//Implementation/ST", options, validate);
        }
        
        /// <summary>
        /// Format old-style Single/Array/TextLines files.
        /// </summary>
        private void FormatOldFormat(FormatterOptions options, bool validate)
        {
            // Format Implementation text (under Object/Implementation/TextDocument/TextLines)
            FormatOldTextLines("//Single[@Name='Object']/Single[@Name='Implementation']/Single[@Name='TextDocument']/Array[@Name='TextLines']", options, validate, isImplementation: true);

            // Format Declaration/Interface text (under Object/Interface/TextDocument/TextLines)
            FormatOldTextLines("//Single[@Name='Object']/Single[@Name='Interface']/Single[@Name='TextDocument']/Array[@Name='TextLines']", options, validate, isImplementation: false);
        }
        
        /// <summary>
        /// Extracts text from old-format TextLines array, formats it, and writes back.
        /// </summary>
        private void FormatOldTextLines(string xpath, FormatterOptions options, bool validate, bool isImplementation)
        {
            var textLinesNode = _doc.SelectSingleNode(xpath);
            if (textLinesNode == null) return;
            
            // Extract text from each Single element's "Text" child
            var lineNodes = new List<XmlNode>();
            var lines = new List<string>();
            
            foreach (XmlNode singleNode in textLinesNode.ChildNodes)
            {
                if (singleNode.Name != "Single") continue;
                
                var textNode = singleNode.SelectSingleNode("Single[@Name='Text']");
                if (textNode != null)
                {
                    lineNodes.Add(textNode);
                    lines.Add(textNode.InnerText);
                }
            }
            
            if (lines.Count == 0) return;
            
            // Join lines into source code
            string sourceCode = string.Join("\n", lines);
            if (string.IsNullOrWhiteSpace(sourceCode)) return;
            
            // Implementation code is a bare statement list; the core formatter
            // parses it natively and keeps outermost statements at column 0
            string formatted = FormatCode(sourceCode, options, validate);
            
            // Split formatted output back into lines
            var formattedLines = formatted.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            
            // Remove trailing empty line if the formatter added one
            if (formattedLines.Length > 0 && string.IsNullOrEmpty(formattedLines[formattedLines.Length - 1]))
            {
                var trimmed = new string[formattedLines.Length - 1];
                Array.Copy(formattedLines, trimmed, trimmed.Length);
                formattedLines = trimmed;
            }
            
            // Write back to Text nodes
            for (int i = 0; i < lineNodes.Count && i < formattedLines.Length; i++)
            {
                lineNodes[i].InnerText = formattedLines[i];
            }
            
            // If we have more formatted lines than original nodes, add new nodes
            if (formattedLines.Length > lineNodes.Count)
            {
                // Get the template Single node structure from the last existing node
                var templateNode = textLinesNode.ChildNodes[textLinesNode.ChildNodes.Count - 1];
                int lastId = GetMaxId(textLinesNode) + 1;
                
                for (int i = lineNodes.Count; i < formattedLines.Length; i++)
                {
                    var newSingle = _doc.CreateElement("Single");
                    newSingle.SetAttribute("Type", templateNode.Attributes?["Type"]?.Value ?? "{a5de0b0b-1cb5-4913-ac21-9d70293ec00d}");
                    newSingle.SetAttribute("Method", "IArchivable");
                    
                    var idElem = _doc.CreateElement("Single");
                    idElem.SetAttribute("Name", "Id");
                    idElem.SetAttribute("Type", "long");
                    idElem.InnerText = lastId++.ToString();
                    newSingle.AppendChild(idElem);
                    
                    var nullTag = _doc.CreateElement("Null");
                    nullTag.SetAttribute("Name", "Tag");
                    newSingle.AppendChild(nullTag);
                    
                    var textElem = _doc.CreateElement("Single");
                    textElem.SetAttribute("Name", "Text");
                    textElem.SetAttribute("Type", "string");
                    textElem.InnerText = formattedLines[i];
                    newSingle.AppendChild(textElem);
                    
                    textLinesNode.AppendChild(newSingle);
                }
            }
            // If we have fewer formatted lines, remove excess nodes
            else if (formattedLines.Length < lineNodes.Count)
            {
                var nodesToRemove = new List<XmlNode>();
                foreach (XmlNode singleNode in textLinesNode.ChildNodes)
                {
                    if (singleNode.Name != "Single") continue;
                    var textNode = singleNode.SelectSingleNode("Single[@Name='Text']");
                    if (textNode != null && !IsNodeInList(textNode, lineNodes, formattedLines.Length))
                        nodesToRemove.Add(singleNode);
                }
                foreach (var node in nodesToRemove)
                    textLinesNode.RemoveChild(node);
            }
        }
        
        private static int GetMaxId(XmlNode textLinesNode)
        {
            int maxId = 0;
            foreach (XmlNode singleNode in textLinesNode.ChildNodes)
            {
                if (singleNode.Name != "Single") continue;
                var idNode = singleNode.SelectSingleNode("Single[@Name='Id']");
                if (idNode != null && int.TryParse(idNode.InnerText, out int id))
                    maxId = Math.Max(maxId, id);
            }
            return maxId;
        }
        
        private static bool IsNodeInList(XmlNode textNode, List<XmlNode> list, int count)
        {
            for (int i = 0; i < count && i < list.Count; i++)
                if (list[i] == textNode) return true;
            return false;
        }
        
        /// <summary>
        /// Format CDATA sections using the standard XPath approach (new format).
        /// </summary>
        private void FormatNodes(string xpath, FormatterOptions options, bool validate)
        {
            var nodes = _doc.SelectNodes(xpath);
            if (nodes == null) return;

            foreach (XmlNode node in nodes)
            {
                string sourceCode = node.InnerText;
                if (string.IsNullOrWhiteSpace(sourceCode)) continue;

                string formatted = FormatCode(sourceCode, options, validate);

                // Write back as CDATA
                node.InnerXml = $"<![CDATA[{formatted}]]>";
            }
        }
        
        /// <summary>
        /// Format CDATA sections that contain bare ST code (implementation bodies).
        /// The core formatter parses bare statement lists natively, so no temporary
        /// PROGRAM wrapper is needed and outermost statements stay at column 0.
        /// </summary>
        private void FormatNodesWrapped(string xpath, FormatterOptions options, bool validate)
        {
            var nodes = _doc.SelectNodes(xpath);
            if (nodes == null) return;

            foreach (XmlNode node in nodes)
            {
                string sourceCode = node.InnerText;
                if (string.IsNullOrWhiteSpace(sourceCode)) continue;

                string formatted = FormatCode(sourceCode, options, validate);

                // Write back as CDATA
                node.InnerXml = $"<![CDATA[{formatted}]]>";
            }
        }
        
        /// <summary>
        /// Runs the formatter pipeline: Lexer → Parser → Formatter, then the
        /// equivalence validation. Validation failure throws before any write-back
        /// happens — the caller's catch skips Save(), so the file on disk stays
        /// untouched (the same guarantee CSharpier's IFormattingValidator gives:
        /// a refused file beats a corrupted one).
        /// </summary>
        private string FormatCode(string sourceCode, FormatterOptions options, bool validate)
        {
            var lexer = new STLexer(sourceCode);
            var tokens = lexer.Tokenize();
            var parser = new STParser(tokens);
            var cst = parser.Parse();
            var formatter = new STFormatterCore.Formatter.STFormatter(options);
            string formatted = formatter.Format(cst, sourceCode);

            if (validate)
            {
                var result = STFormatterCore.Validation.FormattingValidator.Validate(sourceCode, formatted, options);
                if (!result.IsValid)
                    throw new InvalidOperationException(
                        $"等价性校验未通过，已拒绝写回（--skip-validation 可跳过）：{result.FailureMessage}");
            }

            return formatted;
        }
        
        public void Save()
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                NewLineChars = _lineEnding
            };
            using (var stream = new System.IO.FileStream(_path, System.IO.FileMode.Create))
            using (var writer = new System.IO.StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            using (var xmlWriter = XmlWriter.Create(writer, settings))
            {
                _doc.Save(xmlWriter);
            }
        }
    }
}
