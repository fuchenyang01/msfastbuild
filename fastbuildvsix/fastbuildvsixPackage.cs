global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using EnvDTE80;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using OutputWindowPane = EnvDTE.OutputWindowPane;

namespace fastbuildvsix
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(FastbuildOption), "FASTBuild", "General", 0, 0, true)]
    [Guid(PackageGuids.fastbuildvsixString)]
    [ProvideToolWindow(typeof(FastBuildMonitorPane))]
    public sealed class fastbuildvsixPackage : ToolkitPackage
    {
        public DTE2 dte;
        public OutputWindowPane outputPane;
        public string OptionFBArgs
        {
            get
            {
                FastbuildOption page = (FastbuildOption)GetDialogPage(typeof(FastbuildOption));
                return page.FBArgs;
            }
        }
        public bool OptionFBUnity
        {
            get
            {
                FastbuildOption page = (FastbuildOption)GetDialogPage(typeof(FastbuildOption));
                return page.FBUnity;
            }
        }
        public string OptionFBPath
        {
            get
            {
                FastbuildOption page = (FastbuildOption)GetDialogPage(typeof(FastbuildOption));
                return page.FBPath;
            }
        }
        public class VSIXPackageInformation
        {
            public string _version;
            public string _packageName;
            public string _authors;
        }
        public static VSIXPackageInformation GetCurrentVSIXPackageInformation()
        {
            VSIXPackageInformation outInfo = null;

            try
            {
                outInfo = new VSIXPackageInformation
                {
                    _version = Vsix.Version,
                    _authors = Vsix.Author,
                    _packageName = Vsix.Name,
                };
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return outInfo;
        }
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await fastbuild.InitializeAsync(this);
            dte = await GetServiceAsync(typeof(DTE)) as DTE2;
            OutputWindow outputWindow = dte?.ToolWindows.OutputWindow;
            outputPane = outputWindow?.OutputWindowPanes.Add("fastbuild");
            outputPane?.OutputString("FASTBuild\r");
        }
    }
}