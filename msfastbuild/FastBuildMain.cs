using Microsoft.Build.Locator;
using System;
using System.Collections.Generic;
using System.Linq;

namespace msfastbuild
{
    public class FastBuildMain
    {
        private static VisualStudioInstance SelectMSBuildToUse(List<VisualStudioInstance> instances)
        {
            Version ver = null;
            var used = 0;
            for (var i = 1; i <= instances.Count; i++)
            {
                var instance = instances[i - 1];
                if (ver == null || ver < instance.Version)
                {
                    ver = instance.Version;
                    used = i;
                }
            }
            if (used == 0) return null;
            return instances[used - 1];
        }
        static int Main(string[] args)
        {
            // Register the MSBuild instance,使用系统安装的msbuild加载工程,保证
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            var ins = SelectMSBuildToUse(instances);
            Console.WriteLine($"Using MSBuild from VS Instance: {ins.Name} - {ins.Version}");
            Console.WriteLine();
            MSBuildLocator.RegisterInstance(ins);
            return msfastbuild.Run(args);
        }
    }
}