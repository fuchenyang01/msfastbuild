using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using msfastbuild;
using System;
using System.Runtime.InteropServices;

namespace fastbuildvsix
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("7efb24ed-204d-47a6-a770-0e0b8487c6ae")]
    public class FastBuildMonitorPane : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FastBuildMonitorPane"/> class.
        /// </summary>
        public FastBuildMonitorPane() : base(null)
        {
            this.Caption = "FastBuild Monitor";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            var pkginfo = fastbuildvsixPackage.GetCurrentVSIXPackageInformation();
            var monitorControl = new FASTBuildMonitorControl(pkginfo._packageName, pkginfo._version,pkginfo._authors);
            //monitorControl.OnPreviewDocumentClick += MonitorControl_OnPreviewDocumentClick;
            this.Content = monitorControl;
        }
    }
}
