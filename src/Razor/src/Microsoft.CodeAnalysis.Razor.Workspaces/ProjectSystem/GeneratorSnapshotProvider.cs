// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor;

internal interface IGeneratorSnapshotProvider : IWorkspaceService
{
    Task GetGenerateDocumentsAsync(IDocumentSnapshot documentSnapshot);
}

//internal class GeneratorSnapshotProvider : IWorkspaceService
//{
//    public Task<GeneratorSnapshot> GetGeneratorSnapshotAsync(ProjectState project)
//    {
//        return Task.FromResult(new GeneratorSnapshot());
//    }
//}

//internal class GeneratorSnapshot
//{
//    public RazorCodeDocument GetGeneratedSnapshot(string uri) { return null; }
//}
