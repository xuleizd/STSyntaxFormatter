using System;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using TCatSysManagerLib;

namespace STFormatterVSIX
{
    /// <summary>
    /// Helper class for formatting TwinCAT PLC project items via COM interfaces.
    /// Uses ITcPlcDeclaration and ITcPlcImplementation to read/write ST code
    /// directly in the editor buffer (not the disk file).
    ///
    /// Object-type checks (IsPlcObject) are preferred over path suffixes: Action,
    /// Method, Property and Transition open as their own documents whose path does
    /// not end in .TcPOU, so the object type is the only reliable test. This mirrors
    /// TcBlack's behaviour exactly.
    /// </summary>
    internal static class TcPouDocument
    {
        /// <summary>
        /// Checks whether the path is a TwinCAT PLC file: .TcPOU, .TcIO, .TcDUT, or .TcGVL.
        /// </summary>
        public static bool IsTcPouFile(string path)
        {
            return !string.IsNullOrEmpty(path)
                && (path.EndsWith(".TcPOU", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".TcIO", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".TcDUT", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".TcGVL", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks whether a project item object is a TwinCAT PLC object that may be
        /// formatted. Action, Method, Property (Get/Set) and Transition open as their
        /// own documents whose path may not end in .TcPOU, so the object type is the
        /// reliable check. Those objects all implement the TcPlc declaration or
        /// implementation interfaces (or the _ITcPlcDeclImpl marker).
        /// </summary>
        public static bool IsPlcObject(object projectItemObject)
        {
            return projectItemObject is ITcPlcDeclaration
                || projectItemObject is ITcPlcImplementation
                || projectItemObject is _ITcPlcDeclImpl;
        }

        /// <summary>
        /// Formats the declaration and (ST) implementation parts of a TwinCAT
        /// PLC project item — and recursively formats its children (Action, Method,
        /// Property, Transition, Folder, ...) so that those objects are formatted
        /// even though they never open as a .TcPOU file themselves.
        /// This modifies the editor buffer, not the disk file. VS handles the save.
        /// </summary>
        public static bool FormatProjectItem(ProjectItem projectItem, Func<string, string> formatFunc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (projectItem == null || formatFunc == null)
                return false;

            object projectItemObject = projectItem.Object;

            // Object-type check instead of path suffix: a POU's Action/Method/
            // Property/Transition opens with a FullName that is not a .TcPOU path.
            if (!IsPlcObject(projectItemObject) && !IsTcPouFile(projectItem.get_FileNames(1)))
                return false;

            return FormatPlcObject(projectItemObject, formatFunc);
        }

        /// <summary>
        /// Formats the declaration and (ST) implementation of a single PLC object
        /// (POU, Action, Method, Property, Transition, ...) and then recurses into
        /// its children (including folders).
        /// </summary>
        /// <param name="projectItemObject">The object from ProjectItem.Object.</param>
        /// <param name="formatFunc">Lexer → Parser → Formatter pipeline.</param>
        /// <returns>True if any part of the object was changed.</returns>
        private static bool FormatPlcObject(object projectItemObject, Func<string, string> formatFunc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            bool changed = false;

            // Format the declaration part.
            if (projectItemObject is ITcPlcDeclaration declaration)
            {
                string before = declaration.DeclarationText;
                if (!string.IsNullOrEmpty(before))
                {
                    string formatted = formatFunc(before);
                    if (formatted != before)
                    {
                        declaration.DeclarationText = formatted;
                        changed = true;
                    }
                }
            }

            // Format the implementation part, but only for structured text.
            // Other languages (FBD, LD, ...) must not be touched.
            if (projectItemObject is ITcPlcImplementation implementation)
            {
                if (implementation.Language == IECLANGUAGETYPES.IECLANGUAGE_ST)
                {
                    string before = implementation.ImplementationText;
                    if (!string.IsNullOrEmpty(before))
                    {
                        string formatted = formatFunc(before);
                        if (formatted != before)
                        {
                            implementation.ImplementationText = formatted;
                            changed = true;
                        }
                    }
                }
            }

            // Recurse into the children of a POU (Action, Method, Property,
            // Transition, Folder, ...). _ITcSmTreeItem exposes Child/ChildCount on
            // every node (POU as well as Folder), so sub-objects opened on their own
            // are also formatted when their parent POU is saved.
            if (projectItemObject is _ITcSmTreeItem treeItem)
            {
                for (int i = 0; i < treeItem.ChildCount; i++)
                {
                    object child = treeItem.get_Child(i);
                    if (child != null && FormatPlcObject(child, formatFunc))
                        changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// Finds a document by its full path in the DTE documents collection
        /// and formats it via COM interfaces.
        /// </summary>
        public static bool FormatDocument(string fullName, Func<string, string> formatFunc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (dte == null)
                return false;

            foreach (Document doc in dte.Documents)
            {
                if (string.Equals(doc.FullName, fullName, StringComparison.OrdinalIgnoreCase))
                {
                    if (doc.ProjectItem != null)
                    {
                        return FormatProjectItem(doc.ProjectItem, formatFunc);
                    }
                    return false;
                }
            }

            return false;
        }
    }
}
