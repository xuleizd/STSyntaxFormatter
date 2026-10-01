using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using STFormatterVSIX.Commands;
using Task = System.Threading.Tasks.Task;

[assembly: CLSCompliant(false)]

namespace STFormatterVSIX
{
    /// <summary>
    /// VS package that hosts the ST Syntax Formatter extension.
    /// Registers commands, options page, and format-on-save.
    ///
    /// Format-on-save runs on ONE path only (1.8.9): RunningDocEvents.OnBeforeSave
    /// formats the editor buffer, then VS itself saves the formatted buffer. The
    /// old second path (DocumentSaved → format → Task.Delay(150) → save again,
    /// guarded by an autoSaveTriggered flag and a 2-second debounce) was removed:
    /// two paths racing on the same document is a correctness hazard for no gain.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("ST 格式化器", "TwinCAT3 ST 代码格式化工具", "1.8.9")]
    [ProvideOptionPage(typeof(OptionsPage), "ST 格式化", "常规", 0, 0, true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(STFormatterPackage.PackageGuidString)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [SuppressMessage(
        "StyleCop.CSharp.DocumentationRules",
        "SA1650:ElementDocumentationMustBeSpelledCorrectly",
        Justification = "pkgdef, VS and vsixmanifest are valid VS terms"
    )]
    public sealed class STFormatterPackage : AsyncPackage
    {
        /// <summary>
        /// Package GUID string, must match the guidSTFormatterPackage in .vsct.
        /// </summary>
        public const string PackageGuidString = "b2e3c4d5-e6f7-8901-abcd-ef2345678901";

        /// <summary>
        /// Running document table and event sink for format-on-save.
        /// Strong references are required to prevent garbage collection of the sink.
        /// </summary>
        private static RunningDocumentTable runningDocumentTable;
        private static RunningDocEvents runningDocEvents;
        private static uint rdtCookie;

        #region Package Members

        /// <summary>
        /// Package initialization: register commands and set up format-on-save events.
        /// </summary>
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Switch to UI thread for command registration
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Register the format document command
            await FormatDocumentCommand.InitializeAsync(this);

            // Register format-on-save (single path: OnBeforeSave)
            try
            {
                runningDocumentTable = new RunningDocumentTable(this);
                runningDocEvents = new RunningDocEvents(this);
                rdtCookie = runningDocumentTable.Advise(runningDocEvents);
            }
            catch (Exception ex)
            {
                ActivityLog.LogWarning("STFormatter", $"Event registration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the format function that runs the Lexer → Parser → Formatter
        /// pipeline behind three guards (1.8.9, the CSharpier "never corrupt the
        /// buffer" policy):
        ///   1. engine exceptions → keep the original text, show an InfoBar;
        ///   2. empty or unchanged output → keep the original text;
        ///   3. equivalence validation (token/tree/comment comparison) fails →
        ///      keep the original text, show an InfoBar. A refused file always
        ///      beats a corrupted one.
        /// </summary>
        internal static Func<string, string> CreateFormatFunc(OptionsPage options)
        {
            var formatterOptions = options.ToFormatterOptions();
            bool validate = options.ValidateOutput;
            return (string source) =>
            {
                if (string.IsNullOrWhiteSpace(source))
                    return source;

                // TwinCAT hands us fragments in several shapes: a full POU header,
                // a method/property header with VAR blocks, a bare
                // "VAR_INPUT ... END_VAR" variable block, or a bare statement list
                // (ImplementationText without any header). The parser handles all of
                // these at the top level (IsVarKeyword routes every VAR variant to
                // ParseVarBlock, IsBareStatementStart routes statement lists to
                // ParseStatement), so no wrapper is needed.
                string formatted;
                try
                {
                    var lexer = new STFormatterCore.Lexer.STLexer(source);
                    var tokens = lexer.Tokenize();
                    var parser = new STFormatterCore.Parser.STParser(tokens);
                    var cst = parser.Parse();
                    var formatter = new STFormatterCore.Formatter.STFormatter(formatterOptions);
                    formatted = formatter.Format(cst, source);
                }
                catch (Exception ex)
                {
                    ReportFormatFailure($"格式化引擎异常，已保留原文（{ex.Message}）");
                    return source;
                }

                if (string.IsNullOrEmpty(formatted) || formatted == source)
                    return source;

                if (validate)
                {
                    var validation = STFormatterCore.Validation.FormattingValidator.Validate(
                        source, formatted, formatterOptions);
                    if (!validation.IsValid)
                    {
                        ReportFormatFailure($"等价性校验未通过，已保留原文（{validation.FailureMessage}）");
                        return source;
                    }
                }

                return formatted;
            };
        }

        /// <summary>
        /// Surfaces a formatting failure to the user (InfoBar with the engine
        /// version, debounced) and always to the activity log.
        /// </summary>
        [SuppressMessage("Usage", "VSTHRD010",
            Justification = "The method itself dispatches to the UI thread via ThreadHelper.")]
        internal static void ReportFormatFailure(string message)
        {
            try
            {
                ActivityLog.LogWarning("STFormatter", message);
            }
            catch
            {
            }

            if (ThreadHelper.CheckAccess())
            {
                InfoBarService.Show(message);
            }
            else
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    InfoBarService.Show(message);
                });
            }
        }

        #endregion
    }

    /// <summary>
    /// Listens to running document table events to format TwinCAT PLC files
    /// automatically when they are saved. OnBeforeSave formats the editor buffer
    /// via COM interfaces; VS then saves the formatted buffer to disk.
    /// </summary>
    internal sealed class RunningDocEvents : IVsRunningDocTableEvents2
    {
        private readonly AsyncPackage package;

        public RunningDocEvents(AsyncPackage package)
        {
            this.package = package;
        }

        /// <summary>
        /// Called before a document is saved. Formats the document buffer via COM
        /// interfaces so that VS saves the formatted content to disk.
        /// There is no path-suffix gate here on purpose: Action/Method/Property/
        /// Transition documents have monikers that do not end in .TcPOU —
        /// FormatDocument finds the document by moniker and FormatProjectItem
        /// applies the object-type gate (IsPlcObject), so non-PLC documents are a
        /// cheap no-op.
        /// </summary>
        public int OnBeforeSave(uint docCookie)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var rdt = new RunningDocumentTable(package);
                RunningDocumentInfo info = rdt.GetDocumentInfo(docCookie);
                string path = info.Moniker;
                if (string.IsNullOrEmpty(path))
                    return VSConstants.S_OK;

                var options = (OptionsPage)package.GetDialogPage(typeof(OptionsPage));
                if (!options.FormatOnSave)
                    return VSConstants.S_OK;

                // Format the document buffer via COM interfaces before VS saves to disk
                var formatFunc = STFormatterPackage.CreateFormatFunc(options);
                TcPouDocument.FormatDocument(path, formatFunc);
            }
            catch (Exception ex)
            {
                // Never block the save itself.
                STFormatterPackage.ReportFormatFailure($"保存时格式化失败，已保留原文（{ex.Message}）");
            }

            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie) => VSConstants.S_OK;
        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;

        public int OnAfterAttributeChangeEx(
            uint docCookie, uint grfAttribs,
            IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld,
            IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
            => VSConstants.S_OK;

        public int OnBeforeFirstDocumentLock(
            uint docCookie, uint dwRDTLockType,
            uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => VSConstants.S_OK;

        public int OnBeforeLastDocumentUnlock(
            uint docCookie, uint dwRDTLockType,
            uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => VSConstants.S_OK;

        public int OnAfterFirstDocumentLock(
            uint docCookie, uint dwRDTLockType,
            uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => VSConstants.S_OK;

        public int OnBeforeDocumentWindowShow(
            uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
            => VSConstants.S_OK;

        public int OnAfterDocumentWindowHide(
            uint docCookie, IVsWindowFrame pFrame)
            => VSConstants.S_OK;

        public int OnAfterDocumentWindowShow(
            uint docCookie, IVsWindowFrame pFrame)
            => VSConstants.S_OK;
    }
}
