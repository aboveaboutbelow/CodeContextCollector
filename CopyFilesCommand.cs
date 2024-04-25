using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace CodeContextCollector
{
    internal sealed class CopyFilesCommand
    {
        private const int CommandId = 0x0100;
        private static readonly Guid CommandSet = new Guid("722f08d1-568d-4c64-9372-4334257e826d");
        private readonly AsyncPackage _package;

        private CopyFilesCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static CopyFilesCommand Instance { get; private set; }

        private IAsyncServiceProvider ServiceProvider => _package;

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new CopyFilesCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var dte = ServiceProvider.GetServiceAsync(typeof(DTE)).Result as DTE;
                if (dte == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        _package,
                        "Unable to obtain DTE service.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_WARNING,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                if (dte.Documents.Count == 0)
                {
                    VsShellUtilities.ShowMessageBox(
                        _package,
                        "There are no documents open.",
                        "No Open Documents",
                        OLEMSGICON.OLEMSGICON_INFO,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return; // Exit the method as there are no documents to process
                }

                var documents = dte.Documents.Cast<Document>().Where(doc => doc.ActiveWindow != null).ToList();

                var concatenatedContent = "";
                foreach (var doc in documents)
                {
                    var textDocument = (TextDocument)doc.Object("TextDocument");
                    var editPoint = textDocument.StartPoint.CreateEditPoint();
                    var content = editPoint.GetText(textDocument.EndPoint);

                    concatenatedContent += $"{doc.Name}\n```\n{content}\n```\n\n";
                }

                Clipboard.SetText(concatenatedContent);

                // Notify user of success in the status bar instead of a message box
                var statusBar = ServiceProvider.GetServiceAsync(typeof(SVsStatusbar)).Result as IVsStatusbar;
                statusBar?.SetText("CopyFilesCommand: Content copied to clipboard.");
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(
                    _package,
                    $"An error occurred: {ex.Message}",
                    "Error",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}