// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Remote;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class LspRazorGeneratedDocumentProvider : IRazorGeneratedDocumentProvider
{
    readonly ClientNotifierServiceBase _notifier;

    public LspRazorGeneratedDocumentProvider(ClientNotifierServiceBase notifier)
    {
        _notifier = notifier;
    }

    public async Task<(string CSharp, string Html, string Json)> GetGeneratedDocumentAsync(IDocumentSnapshot documentSnapshot)
    {
        var projectRoot = documentSnapshot.Project.FilePath.Substring(0, documentSnapshot.Project.FilePath.LastIndexOf("/"));
        var documentName = GetIdentifierFromPath(documentSnapshot.FilePath?.Substring(projectRoot.Length + 1) ?? "");

        var csharp = await RequestOutput(documentName + ".rsg.cs");
        var html = await RequestOutput(documentName + ".rsg.html");
        var json = await RequestOutput(documentName + ".rsg.json");

        return (csharp, html, json);

        async Task<string> RequestOutput(string name)
        {
            var request = new HostOutputRequest()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = new UriBuilder()
                    {
                        Scheme = Uri.UriSchemeFile,
                        Path = documentSnapshot.FilePath,
                        Host = string.Empty,
                    }.Uri
                },
                GeneratorName = "Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator",
                RequestedOutput = name,
            };

            var response = await _notifier.SendRequestAsync<HostOutputRequest, HostOutputResponse>(RazorLanguageServerCustomMessageTargets.RazorHostOutputsEndpointName, request, CancellationToken.None);
            return response.Output ?? string.Empty;
        }
    }

    //copied from the generator
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

