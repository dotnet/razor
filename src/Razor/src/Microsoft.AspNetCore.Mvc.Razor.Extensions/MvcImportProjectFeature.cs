// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions
{
    internal class MvcImportProjectFeature : RazorProjectEngineFeatureBase, IImportProjectFeature
    {
        public IReadOnlyList<RazorProjectItem> GetImports(RazorProjectItem projectItem)
        {
            if (projectItem == null)
            {
                throw new ArgumentNullException(nameof(projectItem));
            }

            return MvcImportItems.GetImportItems(ProjectEngine.FileSystem, projectItem);
        }
    }
}
