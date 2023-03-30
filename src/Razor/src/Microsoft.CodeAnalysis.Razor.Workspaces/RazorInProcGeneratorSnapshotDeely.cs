// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

[ExportWorkspaceServiceFactory(typeof(IGeneratorSnapshotProvider), ServiceLayer.Default)]
internal class DeelyFactory : IWorkspaceServiceFactory
{
    public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
    {
        return new RazorInProcGeneratorSnapshotDeely();
    }
}

internal class RazorInProcGeneratorSnapshotDeely : IGeneratorSnapshotProvider
{
    public Task<(string CSharp, string Html)> GetGenerateDocumentsAsync(IDocumentSnapshot documentSnapshot)
    {
        return Task.FromResult(("abc", "def"));
    }
}
