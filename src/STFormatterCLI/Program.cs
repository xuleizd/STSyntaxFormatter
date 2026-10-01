using System;
using System.IO;
using System.Collections.Generic;
using CommandLine;
using STFormatterCore.Configuration;

namespace STFormatterCLI
{
    class Program
    {
        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<Options>(args)
                .MapResult(
                    RunFormat,
                    errors => 1
                );
        }
        
        static int RunFormat(Options opts)
        {
            // Build FormatterOptions from CLI options
            var formatterOptions = new FormatterOptions
            {
                IndentSize = opts.Indentation,
                AlignDeclarations = opts.AlignDeclarations,
                KeepEmptyLines = opts.KeepEmptyLines,
            };

            // Optional overrides: unspecified values keep the engine defaults.
            if (opts.UseSpaces.HasValue) formatterOptions.UseSpacesInsteadOfTab = opts.UseSpaces.Value;
            if (opts.OperatorSpacing.HasValue) formatterOptions.OperatorSpacing = opts.OperatorSpacing.Value;
            if (opts.CommaSpacing.HasValue) formatterOptions.CommaSpacing = opts.CommaSpacing.Value;
            if (opts.ParenInnerSpacing.HasValue) formatterOptions.ParenInnerSpacing = opts.ParenInnerSpacing.Value;
            if (opts.MaxLineLength.HasValue) formatterOptions.MaxLineLength = opts.MaxLineLength.Value;
            if (opts.BlankLinesAfterVar.HasValue) formatterOptions.BlankLinesAfterVar = opts.BlankLinesAfterVar.Value;
            if (opts.BlankLinesBeforeEnd.HasValue) formatterOptions.BlankLinesBeforeEnd = opts.BlankLinesBeforeEnd.Value;
            if (opts.BlankLinesAroundBlocks.HasValue) formatterOptions.BlankLinesAroundStatementBlocks = opts.BlankLinesAroundBlocks.Value;
            
            // Line ending
            if (opts.WindowsLineEnding) formatterOptions.LineEnding = LineEnding.CRLF;
            else if (opts.UnixLineEnding) formatterOptions.LineEnding = LineEnding.LF;
            else formatterOptions.LineEnding = LineEnding.Auto;
            
            // Type case
            switch (opts.TypeCase?.ToLower())
            {
                case "upper": formatterOptions.TypeCase = TypeCase.Upper; break;
                case "lower": formatterOptions.TypeCase = TypeCase.Lower; break;
                default: formatterOptions.TypeCase = TypeCase.Preserve; break;
            }
            
            // Determine files to process
            var files = new List<string>();
            
            if (!string.IsNullOrEmpty(opts.File))
            {
                if (File.Exists(opts.File) && TcPouFile.IsSupportedFile(opts.File))
                    files.Add(opts.File);
                else
                {
                    Console.Error.WriteLine($"File not found or not a supported TwinCAT file (.TcPOU/.TcDUT/.TcGVL): {opts.File}");
                    return 1;
                }
            }
            else if (!string.IsNullOrEmpty(opts.Project))
            {
                if (Directory.Exists(opts.Project))
                {
                    foreach (var ext in TcPouFile.SupportedExtensions)
                        files.AddRange(Directory.GetFiles(opts.Project, $"*{ext}", SearchOption.AllDirectories));
                }
                else
                {
                    Console.Error.WriteLine($"Directory not found: {opts.Project}");
                    return 1;
                }
            }
            else
            {
                Console.Error.WriteLine("Please specify --file or --project");
                return 1;
            }
            
            if (files.Count == 0)
            {
                Console.WriteLine("No supported TwinCAT files (.TcPOU/.TcDUT/.TcGVL) found.");
                return 0;
            }
            
            int successCount = 0;
            int failCount = 0;
            
            foreach (var file in files)
            {
                try
                {
                    if (opts.Verbose) Console.WriteLine($"Formatting: {file}");
                    
                    var pouFile = new TcPouFile(file);
                    pouFile.Format(formatterOptions, validate: !opts.SkipValidation);
                    pouFile.Save();
                    
                    successCount++;
                    if (opts.Verbose) Console.WriteLine($"  OK");
                }
                catch (Exception ex)
                {
                    failCount++;
                    Console.Error.WriteLine($"  ERROR: {ex.Message}");
                }
            }
            
            Console.WriteLine($"Formatted {successCount} file(s)" + (failCount > 0 ? $", {failCount} failed" : "") + ".");
            return failCount > 0 ? 1 : 0;
        }
    }
}
