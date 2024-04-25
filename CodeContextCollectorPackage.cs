using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace CodeContextCollector
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class CodeContextCollectorPackage : AsyncPackage
    {
        public const string PackageGuidString = "1b1262e3-81a1-4f25-9221-200e0ab7a1c7";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await CopyFilesCommand.InitializeAsync(this);
            await TypeExplorerCommand.InitializeAsync(this);
        }
    }
}