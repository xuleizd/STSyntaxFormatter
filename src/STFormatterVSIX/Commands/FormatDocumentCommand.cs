using System;
using System.ComponentModel.Design;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace STFormatterVSIX.Commands
{
    /// <summary>
    /// Command handler for formatting ST code in the active document.
    /// Uses COM interfaces (ITcPlcDeclaration, ITcPlcImplementation) to format
    /// Declaration and Implementation parts separately, preserving the editor buffer.
    /// </summary>
    internal sealed class FormatDocumentCommand
    {
        /// <summary>
        /// Command ID matching the .vsct definition.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command set GUID, must match guidSTFormatterPackage in .vsct.
        /// </summary>
        public static readonly Guid CommandSet = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        /// <summary>
        /// VS Package that provides this command.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the command.
        /// </summary>
        private FormatDocumentCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(Execute, menuCommandID);
            menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the singleton instance of the command.
        /// </summary>
        public static FormatDocumentCommand Instance { get; private set; }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new FormatDocumentCommand(package, commandService);
        }

        /// <summary>
        /// Enables the command only when the active document is a TwinCAT PLC object.
        /// Object type is preferred because Action/Method/Property/Transition open on
        /// their own without a .TcPOU path (see TcPouDocument.IsPlcObject).
        /// </summary>
        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var menuCommand = sender as OleMenuCommand;
            if (menuCommand == null) return;

            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (dte?.ActiveDocument == null)
            {
                menuCommand.Enabled = false;
                return;
            }

            string path = dte.ActiveDocument.FullName ?? "";
            object obj = null;
            if (dte.ActiveDocument.ProjectItem != null)
                obj = dte.ActiveDocument.ProjectItem.Object;

            menuCommand.Enabled = TcPouDocument.IsPlcObject(obj) || TcPouDocument.IsTcPouFile(path);
        }

        /// <summary>
        /// Executes the format command on the active document.
        /// Uses COM interfaces to format Declaration and Implementation separately.
        /// Uses undo context for Ctrl+Z support.
        /// </summary>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (dte?.ActiveDocument == null) return;

            string path = dte.ActiveDocument.FullName ?? "";

            // Object type is the reliable gate; fall back to path suffix.
            object obj = null;
            if (dte.ActiveDocument.ProjectItem != null)
                obj = dte.ActiveDocument.ProjectItem.Object;

            if (!TcPouDocument.IsPlcObject(obj) && !TcPouDocument.IsTcPouFile(path))
                return;

            // Get the ProjectItem from the active window
            ProjectItem projectItem = dte.ActiveWindow?.ProjectItem;
            if (projectItem == null) return;

            var options = (OptionsPage)package.GetDialogPage(typeof(OptionsPage));
            var formatFunc = STFormatterPackage.CreateFormatFunc(options);

            // Use undo context so the user can Ctrl+Z the formatting
            dte.UndoContext.Open("Format ST Code");
            try
            {
                // Format via COM interfaces (Declaration + Implementation separately)
                TcPouDocument.FormatProjectItem(projectItem, formatFunc);
            }
            catch (Exception ex)
            {
                ActivityLog.LogWarning("STFormatter", $"Format command failed: {ex.Message}");
            }
            finally
            {
                dte.UndoContext.Close();
            }
        }
    }
}
