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
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Remote;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

[ExportWorkspaceServiceFactory(typeof(IRazorGeneratedDocumentProvider), ServiceLayer.Default)]
internal class InProcRazorGeneratedDocumentProviderFactory : IWorkspaceServiceFactory
{
    public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
    {
        return new InProcRazorGeneratedDocumentProvider(workspaceServices.Workspace);
    }
}

internal class InProcRazorGeneratedDocumentProvider : IRazorGeneratedDocumentProvider
{

    private readonly Workspace _workspace;

    public InProcRazorGeneratedDocumentProvider(Workspace workspace)
    {
        _workspace = workspace;
    }

    public async Task<(string CSharp, string Html, string Json)> GetGeneratedDocumentAsync(IDocumentSnapshot documentSnapshot)
    {
        string? csharp = null;
        string? html = null;
        string? json = null;

        var project = _workspace.CurrentSolution.Projects.FirstOrDefault(p => p.FilePath == documentSnapshot.Project.FilePath);
        if (project is not null)
        {
            // PROTOTYPE: factor this out so we can share its
            var projectRoot = documentSnapshot.Project.FilePath.Substring(0, documentSnapshot.Project.FilePath.LastIndexOf("\\"));
            var documentName = GetIdentifierFromPath(documentSnapshot.FilePath?.Substring(projectRoot.Length + 1) ?? "");

            csharp = await RazorHostOutputHandler.GetHostOutputAsync(project, "Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator", documentName + ".rsg.cs", System.Threading.CancellationToken.None);
            html = await RazorHostOutputHandler.GetHostOutputAsync(project, "Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator", documentName + ".rsg.html", System.Threading.CancellationToken.None);
            json = await RazorHostOutputHandler.GetHostOutputAsync(project, "Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator", documentName + ".rsg.json", System.Threading.CancellationToken.None);
        }

        return (csharp ?? "", html ?? "", json ?? "");
    }

    // PROTOTYPE: copied from the generator
    internal static string GetIdentifierFromPath(string filePath)
    {
        var builder = new StringBuilder(filePath.Length);

        for (var i = 0; i < filePath.Length; i++)
        {
            switch (filePath[i])
            {
                case ':' or '\\' or '/':
                case char ch when !char.IsLetterOrDigit(ch):
                    builder.Append('_');
                    break;
                default:
                    builder.Append(filePath[i]);
                    break;
            }
        }

        return builder.ToString();
    }
}
