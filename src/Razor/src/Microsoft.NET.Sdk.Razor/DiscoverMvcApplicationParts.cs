// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class DiscoverMvcApplicationParts : Task
    {
        [Required]
        public ITaskItem[] MvcAssemblyNames { get; set; }

        [Required]
        public ITaskItem[] ResolvedReferences { get; set; }

        [Output]
        public string[] ApplicationPartAssemblyNames { get; set; }

        public override bool Execute()
        {
            var referenceItems = new List<ResolveReferenceItem>();
            foreach (var item in ResolvedReferences)
            {
                string assemblyName = null;
                var fusionName = item.GetMetadata("FusionName");
                if (!string.IsNullOrEmpty(fusionName))
                {
                    assemblyName = new AssemblyName(fusionName).Name;
                }
                else
                {
                    assemblyName = GetAssemblyName(item.ItemSpec);
                }

                if (assemblyName == null)
                {
                    continue;
                }

                referenceItems.Add(new ResolveReferenceItem
                {
                    AssemblyName = assemblyName,
                    IsSystemReference = item.GetMetadata("IsSystemReference") == "true",
                    Path = item.ItemSpec,
                });
            }

            var mvcAssemblyNames = MvcAssemblyNames.Select(s => s.ItemSpec).ToList();

            var provider = new ApplicationPartsProvider(mvcAssemblyNames, referenceItems);
            var assemblyNames = provider.ResolveAssemblies();

            ApplicationPartAssemblyNames = assemblyNames.Count > 0 ?
                assemblyNames.ToArray() :
                Array.Empty<string>();
            return true;
        }

        // Based on https://github.com/microsoft/msbuild/blob/f82477f7cbc53febd3256a0c3004c34d5b3a0cb2/src/Shared/AssemblyNameExtension.cs#L183
        private string GetAssemblyName(string assemblyPath)
        {
            try
            {
                return AssemblyName.GetAssemblyName(assemblyPath).Name;
            }
            catch (FileLoadException ex)
            {
                Log.LogWarningFromException(ex);

                // Its pretty hard to get here, you need an assembly that contains a valid reference
                // to a dependent assembly that, in turn, throws a FileLoadException during GetAssemblyName.
                // Still it happened once, with an older version of the CLR.

                // ...falling through and relying on the targetAssemblyName==null behavior below...
            }
            catch (FileNotFoundException ex)
            {
                // Its pretty hard to get here, also since we do a file existence check right before calling this method so it can only happen if the file got deleted between that check and this call.
                Log.LogWarningFromException(ex);
            }

            return null;
        }
    }
}
