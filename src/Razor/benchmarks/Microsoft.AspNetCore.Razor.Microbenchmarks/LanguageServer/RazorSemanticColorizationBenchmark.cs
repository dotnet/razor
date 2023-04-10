// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentColor;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.LanguageServer;

public class RazorSemanticColorizationBenchmark : RazorLanguageServerBenchmarkBase
{
    public enum FileTypes
    {
        Small,
        Large
    }

    private string? _filePath;
    private Uri? DocumentUri { get; set; }
    private DocumentColorEndpoint? DocumentColorEndpoint { get; set; }
    private IDocumentSnapshot? DocumentSnapshot { get; set; }
    private RazorRequestContext RazorRequestContext { get; set; }

    [ParamsAllValues] public FileTypes FileType { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var languageServer = RazorLanguageServer.GetInnerLanguageServerForTesting();

        DocumentColorEndpoint = new DocumentColorEndpoint(new ClientNotifierService());
        var projectRoot = Path.Combine(RepoRoot, "src", "Razor", "test", "testapps", "ComponentApp");
        var projectFilePath = Path.Combine(projectRoot, "ComponentApp.csproj");
        _filePath = Path.Combine(projectRoot, "Components", "Pages", "Generated.razor");

        var content = GetFileContents(FileType);
        File.WriteAllText(_filePath, content);

        var targetPath = "/Components/Pages/Generated.razor";

        DocumentUri = new Uri(_filePath);
        DocumentSnapshot = GetDocumentSnapshot(projectFilePath, _filePath, targetPath);
        var documentContext = new VersionedDocumentContext(DocumentUri, DocumentSnapshot, 1);

        RazorRequestContext = new RazorRequestContext(documentContext, Logger, languageServer.GetLspServices());
    }


    private protected override LanguageServerFeatureOptions BuildFeatureOptions()
    {
        return new ColorizationLanguageServerFeatureOptions();
    }

    private string GetFileContents(FileTypes fileType)
    {
        var sb = new StringBuilder();

        sb.Append("""
            @using System;
            """);

        for (var i = 0; i < (fileType == FileTypes.Small ? 1 : 100); i++)
        {
            sb.Append($$"""
            @{
                var y{{i}} = 456;
            }

            <div>
                <p>Hello there Mr {{i}}</p>
                <div>
                    <span>hi!</span>
                </div>
            </div>
            """);
        }

        return sb.ToString();
    }

    [Benchmark(Description = "SemanticColorizationNoLsp")]
    public async Task SemanticColorizationNoLspAsync()
    {
        var request = new DocumentColorParams { TextDocument = new TextDocumentIdentifier { Uri = DocumentUri! } };

        await DocumentColorEndpoint!.HandleRequestAsync(request, RazorRequestContext, CancellationToken.None);
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        File.Delete(_filePath!);

        var innerServer = RazorLanguageServer.GetInnerLanguageServerForTesting();

        await innerServer.ShutdownAsync();
        await innerServer.ExitAsync();
    }

    private class ColorizationLanguageServerFeatureOptions : LanguageServerFeatureOptions
    {
        public override bool SupportsFileManipulation => true;

        public override string ProjectConfigurationFileName => "project.razor.json";

        public override string CSharpVirtualDocumentSuffix => ".ide.g.cs";

        public override string HtmlVirtualDocumentSuffix => "__virtual.html";

        public override bool SingleServerCompletionSupport => true;

        public override bool SingleServerSupport => true;

        public override bool SupportsDelegatedCodeActions => true;

        public override bool ReturnCodeActionAndRenamePathsWithPrefixedSlash => true;

        public override bool ShowAllCSharpCodeActions => true;
    }

    private class ClientNotifierService : ClientNotifierServiceBase
    {
        public override Task OnInitializedAsync(VSInternalClientCapabilities clientCapabilities,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override Task SendNotificationAsync<TParams>(string method, TParams @params,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task SendNotificationAsync(string method, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<TResponse> SendRequestAsync<TParams, TResponse>(string method, TParams @params,
            CancellationToken cancellationToken)
        {
            object response = new[] { new ColorInformation() };
            return Task.FromResult<TResponse>((TResponse)response);
        }
    }
}
