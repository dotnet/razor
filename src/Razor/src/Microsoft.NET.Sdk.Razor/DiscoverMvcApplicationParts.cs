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
                const string FusionNameKey = "FusionName";
                var fusionName = item.GetMetadata(FusionNameKey);
                if (string.IsNullOrEmpty(fusionName))
                {
                    Log.LogError($"Missing required metadata '{FusionNameKey}' for '{item.ItemSpec}.");
                    return false;
                }

                var assemblyName = new AssemblyName(fusionName).Name;
                referenceItems.Add(new ResolveReferenceItem
                {
                    AssemblyName = assemblyName,
                    IsSystemReference = item.GetMetadata("IsSystemReference") == "true",
                    Path = item.ItemSpec,
                });
            }

            var mvcAssemblyNames = MvcAssemblyNames.Select(s => s.ItemSpec).ToList();

            var provider = new ReferencesToMvcResolver(mvcAssemblyNames, referenceItems);
            var assemblyNames = provider.ResolveAssemblies();

            ApplicationPartAssemblyNames = assemblyNames.Count > 0 ?
                assemblyNames.ToArray() :
                Array.Empty<string>();

            return !Log.HasLoggedErrors;
        }
    }
}
