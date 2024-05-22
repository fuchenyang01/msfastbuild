using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace fastbuildvsix
{
    [Guid("5ba06926-ab9a-4256-992c-277793b13e15")]
    [ComVisible(true)]
    public sealed class FastbuildOption: DialogPage
    {
        private string fbArgs = "-dist -ide -monitor";
        private string fbPath = "FBuild.exe";
        private bool fbUnity = false;

        [Category("Options")]
        [DisplayName("FASTBuild arguments")]
        [Description("Arguments that will be passed to FASTBuild, default \"-dist -ide -monitor\"")]
        public string FBArgs
        {
            get => fbArgs;
            set => fbArgs = value;
        }

        [Category("Options")]
        [DisplayName("FBuild.exe path")]
        [Description("Specify the path to FBuild.exe")]
        public string FBPath
        {
            get => fbPath;
            set => fbPath = value;
        }

        [Category("Options")]
        [DisplayName("Use unity files")]
        [Description("Whether to merge files together to speed up compilation. May require modifying some headers.")]
        public bool FBUnity
        {
            get => fbUnity;
            set => fbUnity = value;
        }
    }
}
