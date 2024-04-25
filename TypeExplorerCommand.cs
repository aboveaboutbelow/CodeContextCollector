using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Document = EnvDTE.Document;
using Task = System.Threading.Tasks.Task;
using TextDocument = EnvDTE.TextDocument;

namespace CodeContextCollector
{
    internal sealed class TypeExplorerCommand
    {
        private const int CommandId = 0x0101;
        private static readonly Guid CommandSet = new Guid("722f08d1-568d-4c64-9372-4334257e826d");
        private readonly AsyncPackage _package;

        private TypeExplorerCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        private static TypeExplorerCommand Instance { get; set; }
        private IAsyncServiceProvider ServiceProvider => _package;

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            if (await package.GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                Instance = new TypeExplorerCommand(package, commandService);
            }
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Task.Run(async () =>
            {
                try
                {
                    await ExecuteAsync();
                }
                catch (Exception ex)
                {
                    await HandleExceptionAsync(ex, "An error occurred while executing the command.");
                }
            });
        }

        private async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await ServiceProvider.GetServiceAsync(typeof(DTE)) as DTE2;
            Assumes.Present(dte);

            var activeDocument = dte.ActiveDocument;
            if (activeDocument == null || !activeDocument.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                VsShellUtilities.ShowMessageBox(
                    _package,
                    "Please open a C# file before running this command.",
                    "TypeExplorer",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            await AnalyzeDocumentAsync(activeDocument);
        }

        private async Task AnalyzeDocumentAsync(Document activeDocument)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var textDocument = activeDocument.Object("TextDocument") as TextDocument;
            Assumes.Present(textDocument);

            var componentModel = await ServiceProvider.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            Assumes.Present(componentModel);
            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            Assumes.Present(workspace);

            var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(activeDocument.FullName).FirstOrDefault();
            if (documentId == null)
            {
                return;
            }

            var roslynDocument = workspace.CurrentSolution.GetDocument(documentId);
            if (roslynDocument == null)
            {
                return;
            }

            var semanticModel = await roslynDocument.GetSemanticModelAsync();
            var root = await semanticModel.SyntaxTree.GetRootAsync();

            var typeCollector = new TypeCollector(semanticModel);
            typeCollector.Visit(root);

            var typeSymbols = typeCollector.TypeSymbols;

            var openedFiles = new HashSet<string>();
            var notFoundTypes = new HashSet<string>();
            var typesInCurrentFile = new HashSet<string>();
            var alreadyOpenFiles = new HashSet<string>();
            var typesProcessed = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var typeSymbol in typeSymbols)
            {
                await ProcessTypeSymbolAsync(typeSymbol, activeDocument.FullName, openedFiles, notFoundTypes, typesInCurrentFile, alreadyOpenFiles, typesProcessed);
            }
        }

        private async Task ProcessTypeSymbolAsync(
            INamedTypeSymbol typeSymbol,
            string activeDocumentPath,
            HashSet<string> openedFiles,
            HashSet<string> notFoundTypes,
            HashSet<string> typesInCurrentFile,
            HashSet<string> alreadyOpenFiles,
            HashSet<INamedTypeSymbol> typesProcessed)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (typeSymbol == null || !typesProcessed.Add(typeSymbol))
            {
                return;
            }

            if (!typeSymbol.Locations.Any(loc => loc.IsInSource))
            {
                notFoundTypes.Add(typeSymbol.Name);
                return;
            }

            foreach (var location in typeSymbol.Locations)
            {
                if (location.IsInSource)
                {
                    var filePath = location.SourceTree.FilePath;
                    if (filePath.EndsWith(".g.cs"))
                    {
                        continue;
                    }

                    if (string.Equals(filePath, activeDocumentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        typesInCurrentFile.Add(typeSymbol.Name);
                    }
                    else
                    {
                        await OpenFileAsync(filePath, openedFiles, alreadyOpenFiles);
                    }
                }
            }

            if (typeSymbol.BaseType != null)
            {
                await ProcessTypeSymbolAsync(typeSymbol.BaseType, activeDocumentPath, openedFiles, notFoundTypes, typesInCurrentFile, alreadyOpenFiles, typesProcessed);
            }

            foreach (var interfaceType in typeSymbol.Interfaces)
            {
                await ProcessTypeSymbolAsync(interfaceType, activeDocumentPath, openedFiles, notFoundTypes, typesInCurrentFile, alreadyOpenFiles, typesProcessed);
            }
        }

        private async Task OpenFileAsync(string filePath, HashSet<string> openedFiles, HashSet<string> alreadyOpenFiles)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await ServiceProvider.GetServiceAsync(typeof(DTE)) as DTE2;
            Assumes.Present(dte);

            foreach (Document doc in dte.Documents)
            {
                if (string.Equals(doc.FullName, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    doc.Activate();
                    alreadyOpenFiles.Add(filePath);
                    return;
                }
            }

            var newWindow = dte.ItemOperations.OpenFile(filePath);
            newWindow.Activate();

            openedFiles.Add(filePath);
        }

        private async Task HandleExceptionAsync(Exception ex, string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            VsShellUtilities.ShowMessageBox(
                _package,
                $"{message}\n{ex.Message}",
                "TypeExplorer Error",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}