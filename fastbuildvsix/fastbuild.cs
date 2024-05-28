using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.VCProjectEngine;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Project = EnvDTE.Project;
using Solution = EnvDTE.Solution;

namespace fastbuildvsix
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class fastbuild
    {
        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="fastbuild"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private fastbuild(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.FASTBuildId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.SlnFASTBuildId);
            menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.ContextMenuFASTBuildId);
            menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.ContextMenuFASTBuildClearId);
            menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
            
            menuCommandID = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.SlnContextMenuFASTBuildId);
            menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.SlnContextMenuFASTBuildClearId);
            menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.SlnMenuFASTBuildClearId);
            menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.CancelFASTBuild);
            menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.ContextMenuFASTBuildProjectId);
            menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            var menuCommandID1 = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.FASTBuildMonitorCommandId);
            var menuItem1 = new MenuCommand(this.ShowToolWindow, menuCommandID1);
            commandService.AddCommand(menuItem1);

            var menuCommandID2 = new CommandID(PackageGuids.guidfastbuildvsixPackageCmdSet, PackageIds.FASTBuildMonitorToolsCommandId);
            var menuItem2 = new MenuCommand(this.ShowToolWindow, menuCommandID2);
            commandService.AddCommand(menuItem2);
        }


        private System.Diagnostics.Process FBProcess
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static fastbuild Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }
        private static IVsSolution solution;
        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in fastbuild's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            solution = await package.GetServiceAsync<SVsSolution, IVsSolution>();
            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new fastbuild(package, commandService);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern int GetSystemDefaultLCID();
        private bool IsFbuildFindable(string path)
        {
            string fbuild = "fbuild.exe";

            if (path.ToLower() != fbuild)
            {
                return File.Exists(path);
            }

            string pathVariable = Environment.GetEnvironmentVariable("PATH");
            foreach (string searchPath in pathVariable.Split(Path.PathSeparator))
            {
                try
                {
                    string potentialPath = Path.Combine(searchPath, fbuild);
                    if (File.Exists(potentialPath))
                    {
                        return true;
                    }
                }
                catch (ArgumentException)
                { }
            }
            return false;
        }
        private IVsBuildPropertyStorage GetMSBuildPropertyStorage(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int hr = solution.GetProjectOfUniqueName(project.FullName, out var hierarchy);
            System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(hr);
            return hierarchy as IVsBuildPropertyStorage;
        }

        private string GetMSBuildProperty(string key, IVsBuildPropertyStorage storage)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int hr = storage.GetPropertyValue(key, null, (uint)_PersistStorageType.PST_USER_FILE, out var value);
            int E_XML_ATTRIBUTE_NOT_FOUND = unchecked((int)0x8004C738);

            // ignore this HR, it means that there's no value for this key
            if (hr != E_XML_ATTRIBUTE_NOT_FOUND)
            {
                System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(hr);
            }
            return value;
        }
        internal const int CTRL_C_EVENT = 0;
        [DllImport("kernel32.dll")]
        internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        // Delegate type to be used as the Handler Routine for SCCH
        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);
        
        void ClearVcProject(fastbuildvsixPackage pkg,VCProject vc)
        {
            if (vc == null) return;
            var path = Path.GetDirectoryName(vc.ProjectFile);
            if (path == null) return;
            pkg.outputPane.OutputString($"path {path}.\n");
            foreach (string file in Directory.GetFiles(path, "*.bff"))
            {
                pkg.outputPane.OutputString($"remove file {file}.\n");
                File.Delete(file);
            }
            foreach (string file in Directory.GetFiles(path, "*.fdb"))
            {
                pkg.outputPane.OutputString($"remove file {file}.\n");
                File.Delete(file);
            }
            foreach (string file in Directory.GetFiles(path, "*.bat"))
            {
                pkg.outputPane.OutputString($"remove file {file}.\n");
                File.Delete(file);
            }
        }
        static List<Project> GetProjects(Solution sln)
        {
            List<Project> list = new List<Project>();
            list.AddRange(sln.Projects.Cast<Project>());

            for (int i = 0; i < list.Count; i++)
                // OfType will ignore null's.
                list.AddRange(list[i].ProjectItems.Cast<ProjectItem>().Select(x => x.SubProject).OfType<Project>());

            return list;
        }
        private void Execute(object sender, EventArgs e)
        {
            fastbuildvsixPackage pkg = (fastbuildvsixPackage)this.package;
            if (pkg.dte.Solution == null) return;
            ThreadHelper.ThrowIfNotOnUIThread();
            MenuCommand evtSender = sender as MenuCommand;
            pkg.outputPane.Activate();
            pkg.outputPane.Clear();
            Solution sln = pkg.dte.Solution;
            SolutionBuild sb = sln.SolutionBuild;
            SolutionConfiguration2 slnConfig = (SolutionConfiguration2)sb.ActiveConfiguration;
            if (evtSender == null)
            {
                pkg.outputPane.OutputString("VSIX failed to cast sender to OleMenuCommand.\r");
                return;
            }
            if (pkg.dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
            {
                pkg.outputPane.OutputString("Build not launched due to active debugger.\r");
                return;
            }
            if (!IsFbuildFindable(pkg.OptionFBPath))
            {
                pkg.outputPane.OutputString(string.Format("Could not find fbuild at the provided path: {0}, please verify in the msfastbuild options.\r", pkg.OptionFBPath));
                return;
            }
            pkg.dte.ExecuteCommand("File.SaveAll");
            Window window = pkg.dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
            window.Activate();

            string fbCommandLine = "";
            string fbWorkingDirectory = "";

            VCProject vcProj = null;

            //取消生成
            if (evtSender.CommandID.ID == PackageIds.CancelFASTBuild) 
            {
                if (FBProcess == null)
                {
                    return;
                }

                if (FBProcess.HasExited)
                {
                    return;
                }

                FBProcess.CancelOutputRead();
                FBProcess.Kill();

                pkg.outputPane.OutputString($" try cancel last fbuild process\n");
                System.Diagnostics.Process[] localByName = System.Diagnostics.Process.GetProcessesByName("fbuild");
                foreach (System.Diagnostics.Process p in localByName)
                {
                    pkg.outputPane.OutputString($"process:{p.Id} {p.ProcessName}\n");
                    //触发ctrl+c
                    if (AttachConsole((uint)p.Id))
                    {
                        SetConsoleCtrlHandler(null, true);
                        try
                        {
                            pkg.outputPane.OutputString($"ctrl c to process {p.Id}\n");
                            GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);
                        }
                        finally
                        {
                            SetConsoleCtrlHandler(null, false);
                            FreeConsole();
                        }
                    }
                }

                pkg.outputPane.OutputString("构建已取消.\r");

                Window output_window = pkg.dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
                output_window.Activate();
                return;
            }
            //删除生成的fbb文件
            else if(evtSender.CommandID.ID == PackageIds.ContextMenuFASTBuildClearId)
            {
                if (pkg.dte.SelectedItems.Count > 0)
                {
                    //删除选中的项目
                    Project envProj = (pkg.dte.SelectedItems.Item(1).Project as EnvDTE.Project);
                    var vc = (envProj.Object as VCProject);
                    ClearVcProject(pkg, vc);
                }
                pkg.outputPane.OutputString("========== 清理: 完成 ==========\r");
                Window output_window = pkg.dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
                output_window.Activate();
                return;
            }
            else if(evtSender.CommandID.ID == PackageIds.SlnContextMenuFASTBuildClearId || evtSender.CommandID.ID == PackageIds.SlnMenuFASTBuildClearId)
            {
                fbWorkingDirectory = Path.GetDirectoryName(sln.FileName);
                var projs = GetProjects(sln);
                foreach (var proj in projs)
                {
                    ClearVcProject(pkg, proj.Object as VCProject);
                }
                pkg.outputPane.OutputString("==========  清理: 完成 ==========\r");
                Window output_window = pkg.dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
                output_window.Activate();
                return;
            }
            //编译启动工程
            else if(evtSender.CommandID.ID == PackageIds.FASTBuildId || evtSender.CommandID.ID == PackageIds.ContextMenuFASTBuildId || evtSender.CommandID.ID == PackageIds.ContextMenuFASTBuildProjectId)
            {
                if (evtSender.CommandID.ID == PackageIds.FASTBuildId)
                {
                    string startupProject = "";
                    foreach (String item in (Array)sb.StartupProjects)
                    {
                        startupProject += item;
                    }
                    vcProj = sln.Item(startupProject).Object as VCProject;
                }
                else if (evtSender.CommandID.ID == PackageIds.ContextMenuFASTBuildId || evtSender.CommandID.ID == PackageIds.ContextMenuFASTBuildProjectId)
                {
                    if (pkg.dte.SelectedItems.Count > 0)
                    {
                        Project envProj = (pkg.dte.SelectedItems.Item(1).Project as EnvDTE.Project);

                        if (envProj != null)
                        {
                            vcProj = envProj.Object as VCProject;
                        }
                    }
                }
                if (vcProj == null)
                {
                    pkg.outputPane.OutputString("No valid vcproj selected for building or set as the startup project.\r");
                    return;
                }
                pkg.outputPane.OutputString("Building " + Path.GetFileName(vcProj.ProjectFile) + " " + slnConfig.Name + " " + slnConfig.PlatformName + "\r");
                fbCommandLine = string.Format("-p \"{0}\" -c {1} -f {2} -s \"{3}\" -a\"{4}\" -b \"{5}\"", Path.GetFileName(vcProj.ProjectFile), slnConfig.Name, slnConfig.PlatformName, sln.FileName, pkg.OptionFBArgs, pkg.OptionFBPath);
                fbWorkingDirectory = Path.GetDirectoryName(vcProj.ProjectFile);
                //编译单个工程
                if (evtSender.CommandID.ID == PackageIds.ContextMenuFASTBuildProjectId)
                {
                    fbCommandLine += " -o true";
                }
            }
            //编译解决方案
            else if(evtSender.CommandID.ID== PackageIds.SlnFASTBuildId || evtSender.CommandID.ID == PackageIds.SlnContextMenuFASTBuildId)
            {
                fbCommandLine = string.Format("-s \"{0}\" -c {1} -f {2} -a\"{3}\" -b \"{4}\"", sln.FileName, slnConfig.Name, slnConfig.PlatformName, pkg.OptionFBArgs, pkg.OptionFBPath);
                fbWorkingDirectory = Path.GetDirectoryName(sln.FileName);
            }
            if (pkg.OptionFBUnity)
            {
                fbCommandLine += " -u true";
            }
            //找到msfastbuild的路径
            string msfastbuildPath = Assembly.GetAssembly(typeof(msfastbuild.msfastbuild)).Location;
            try
            {
                pkg.outputPane.OutputString("Launching msfastbuild with command line: " + fbCommandLine + "\r");

                FBProcess = new System.Diagnostics.Process();
                FBProcess.StartInfo.FileName = msfastbuildPath;
                FBProcess.StartInfo.Arguments = fbCommandLine;
                FBProcess.StartInfo.WorkingDirectory = fbWorkingDirectory;
                FBProcess.StartInfo.RedirectStandardOutput = true;
                FBProcess.StartInfo.UseShellExecute = false;
                FBProcess.StartInfo.CreateNoWindow = true;
                //var store = GetMSBuildPropertyStorage(sln.Projects.Item(1));
                //pkg.outputPane.OutputString($"store\n");
                ////msfastbuild,值读取出来有问题,这里设置到环境变量
                //FBProcess.StartInfo.EnvironmentVariables["VC_ExecutablePath_x86_x86"] = GetMSBuildProperty("VC_ExecutablePath_x86_x86", store);
                //FBProcess.StartInfo.EnvironmentVariables["VC_ExecutablePath_x64_x64"] = GetMSBuildProperty("VC_ExecutablePath_x64_x64", store);
                //FBProcess.StartInfo.EnvironmentVariables["VCTargetsPath"] = GetMSBuildProperty("VCTargetsPath", store);
                //FBProcess.StartInfo.EnvironmentVariables["WindowsTargetPlatformVersion"] = GetMSBuildProperty("WindowsTargetPlatformVersion", store);
                //FBProcess.StartInfo.EnvironmentVariables["VSInstallDir"] = GetMSBuildProperty("VSInstallDir", store);
                //FBProcess.StartInfo.EnvironmentVariables["VCInstallDir"] = GetMSBuildProperty("VCInstallDir", store);
                //FBProcess.StartInfo.EnvironmentVariables["IncludePath"] = GetMSBuildProperty("IncludePath", store);
                //FBProcess.StartInfo.EnvironmentVariables["LibraryPath"] = GetMSBuildProperty("LibraryPath", store);
                //FBProcess.StartInfo.EnvironmentVariables["ReferencePath"] = GetMSBuildProperty("ReferencePath", store);
                //FBProcess.StartInfo.EnvironmentVariables["Path"] = GetMSBuildProperty("Path", store);
                //pkg.outputPane.OutputString($"environ:WindowsTargetPlatformVersion-{FBProcess.StartInfo.EnvironmentVariables["WindowsTargetPlatformVersion"]} VSInstallDir-{FBProcess.StartInfo.EnvironmentVariables["VSInstallDir"]}\n");
                var SystemEncoding = System.Globalization.CultureInfo.GetCultureInfo(GetSystemDefaultLCID()).TextInfo.OEMCodePage;
                FBProcess.StartInfo.StandardOutputEncoding = Console.OutputEncoding;

                System.Diagnostics.DataReceivedEventHandler OutputEventHandler = (Sender, Args) => {
                    pkg.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        if (Args.Data != null) pkg.outputPane.OutputString(Args.Data + "\r");
                    });
                };

                FBProcess.OutputDataReceived += OutputEventHandler;
                FBProcess.Start();
                FBProcess.BeginOutputReadLine();
                //FBProcess.WaitForExit();
                //ShowMonitorWindow();
            }
            catch (Exception ex)
            {
                pkg.outputPane.OutputString($"VSIX exception launching msfastbuild. Could be a broken VSIX? Exception: {ex}\r");
                pkg.outputPane.OutputString($"trace: {ex.StackTrace}\r");
            }
        }
        private void ShowMonitorWindow()
        {
            ToolWindowPane window = this.package.FindToolWindow(typeof(FastBuildMonitorPane), 0, true);
            if (window?.Frame == null)
            {
                throw new NotSupportedException("Cannot create tool window");
            }
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
        private void ShowToolWindow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ShowMonitorWindow();
        }
    }
}
