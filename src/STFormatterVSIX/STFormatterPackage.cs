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
    /// Registers commands, options page, and format-on-save events.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("ST 格式化器", "TwinCAT3 ST 代码格式化工具", "1.0.0")]
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

        /// <summary>
        /// DTE document events for format-on-save via DocumentSaved.
        /// </summary>
        private static DocumentEvents documentEvents;

        /// <summary>
        /// Path and time of the last automatic save, used to prevent loops.
        /// </summary>
        private static string lastAutoSavePath;
        private static DateTime lastAutoSaveTime = DateTime.MinValue;

        /// <summary>
        /// True while an automatic save triggered by us is in flight.
        /// The DocumentSaved event raised by that save is ignored.
        /// </summary>
        private static bool autoSaveTriggered;

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

            // Register format-on-save events
            try
            {
                runningDocumentTable = new RunningDocumentTable(this);
                runningDocEvents = new RunningDocEvents(this);
                rdtCookie = runningDocumentTable.Advise(runningDocEvents);

                DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte != null)
                {
                    documentEvents = dte.Events.DocumentEvents;
                    documentEvents.DocumentSaved += OnDocumentSaved;
                }
            }
            catch (Exception ex)
            {
                ActivityLog.LogWarning("STFormatter", $"Event registration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after a document was saved. Formats the document via COM interfaces
        /// (modifies editor buffer) and schedules one more save so the formatted content
        /// is written to disk. The save is deferred until the DocumentSaved event has
        /// finished dispatching, otherwise it would dead-lock the UI thread.
        /// The DocumentSaved event raised by that automatic save is skipped via autoSaveTriggered.
        /// </summary>
        private void OnDocumentSaved(Document document)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                HandleFormatOnSave(this, document);
            }
            catch (Exception)
            {
                // Silently ignore formatting errors on save
            }
        }

        /// <summary>
        /// Handles format-on-save: formats TwinCAT PLC files (.TcPOU, .TcDUT, .TcGVL)
        /// via COM interfaces and re-saves if changed.
        /// Uses debouncing and autoSaveTriggered flag to prevent loops.
        /// </summary>
        internal static void HandleFormatOnSave(AsyncPackage package, Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string path = document.FullName;

            // The DocumentSaved raised by our own automatic save: reset the
            // flag and do nothing, the document was already formatted and saved.
            if (autoSaveTriggered)
            {
                autoSaveTriggered = false;
                return;
            }

            if (document.ProjectItem == null)
                return;

            // Object-type check first (Action/Method/Property/Transition opened on
            // their own don't end in .TcPOU); fall back to path suffix. This mirrors
            // TcBlack's IsPlcObject gate exactly.
            if (!TcPouDocument.IsPlcObject(document.ProjectItem.Object)
                && !TcPouDocument.IsTcPouFile(path))
                return;

            // Check if format-on-save is enabled
            var options = (OptionsPage)package.GetDialogPage(typeof(OptionsPage));
            if (!options.FormatOnSave)
                return;

            try
            {
                // Format via COM interfaces (modifies editor buffer, not disk file)
                var formatFunc = CreateFormatFunc(options);
                bool changed = TcPouDocument.FormatProjectItem(document.ProjectItem, formatFunc);
                if (!changed)
                    return;

                // Debounce: never auto-save the same document twice quickly
                bool recentlySaved =
                    string.Equals(lastAutoSavePath, path, StringComparison.OrdinalIgnoreCase)
                    && (DateTime.Now - lastAutoSaveTime).TotalSeconds < 2;
                if (recentlySaved)
                    return;

                lastAutoSavePath = path;
                lastAutoSaveTime = DateTime.Now;
                autoSaveTriggered = true;

                // Defer save to avoid deadlocking the UI thread
                Task.Run(async () =>
                {
                    await Task.Delay(150);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    try
                    {
                        document.Save("");
                    }
                    catch (Exception)
                    {
                    }
                });
            }
            catch (Exception ex)
            {
                ActivityLog.LogWarning("STFormatter", $"Format on save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the format function that runs the Lexer → Parser → Formatter pipeline.
        /// </summary>
        internal static Func<string, string> CreateFormatFunc(OptionsPage options)
        {
            var formatterOptions = options.ToFormatterOptions();
            return (string source) =>
            {
                if (string.IsNullOrWhiteSpace(source))
                    return source;

                // TwinCAT hands us fragments in several shapes: a full POU header,
                // a method/property header with VAR blocks, or a bare
                // "VAR_INPUT ... END_VAR" variable block. The parser handles all of
                // these at the top level (IsVarKeyword routes every VAR variant to
                // ParseVarBlock), so no wrapper is needed.
                var lexer = new STFormatterCore.Lexer.STLexer(source);
                var tokens = lexer.Tokenize();
                var parser = new STFormatterCore.Parser.STParser(tokens);
                var cst = parser.Parse();
                var formatter = new STFormatterCore.Formatter.STFormatter(formatterOptions);
                return formatter.Format(cst, source);
            };
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
        /// Called before a document is saved. Formats the editor buffer via COM interfaces
        /// so that VS saves the formatted content to disk.
        /// </summary>
        public int OnBeforeSave(uint docCookie)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var rdt = new RunningDocumentTable(package);
                RunningDocumentInfo info = rdt.GetDocumentInfo(docCookie);
                string path = info.Moniker;

                if (!TcPouDocument.IsTcPouFile(path))
                    return VSConstants.S_OK;

                var options = (OptionsPage)package.GetDialogPage(typeof(OptionsPage));
                if (!options.FormatOnSave)
                    return VSConstants.S_OK;

                // Format the document buffer via COM interfaces before VS saves to disk
                var formatFunc = STFormatterPackage.CreateFormatFunc(options);
                TcPouDocument.FormatDocument(path, formatFunc);
            }
            catch (Exception)
            {
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
            uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
            => VSConstants.S_OK;
    }
}
