// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.LanguageServer;

[ShortRunJob]
public class RazorDiagnosticsBenchmark : RazorLanguageServerBenchmarkBase
{
    private readonly Range _inRange = new Range { Start = new Position(10, 19), End = new Position(10, 23) };
    private readonly Range _outRange = new Range { Start = new Position(7, 8), End = new Position(7, 15) };
    private string? _filePath;
    private DocumentPullDiagnosticsEndpoint? _documentPullDiagnosticsEndpoint;
    private RazorRequestContext _razorRequestContext;
    private VSInternalDocumentDiagnosticsParams? _request;
    private IEnumerable<VSInternalDiagnosticReport?>? _diagnostics;

    public enum FileTypes
    {
        Small,
        Large
    }

    [Params(0, 1, 1000)]
    public int N { get; set; }

    [ParamsAllValues]
    public FileTypes FileType { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var languageServer = RazorLanguageServer.GetInnerLanguageServerForTesting();
        var languageServerFeatureOptions = BuildFeatureOptions();
        var razorDocumentMappingService = BuildRazorDocumentMappingService();
        var loggerFactory = BuildLoggerFactory();
        var translateDiagnosticsService = new RazorTranslateDiagnosticsService(razorDocumentMappingService, loggerFactory);

        _documentPullDiagnosticsEndpoint = new DocumentPullDiagnosticsEndpoint(
            languageServerFeatureOptions,
            translateDiagnosticsService,
            languageServer: new ClientNotifierService(BuildDiagnostics(N)),
            telemetryReporter: null);
        var projectRoot = Path.Combine(RepoRoot, "src", "Razor", "test", "testapps", "ComponentApp");
        var projectFilePath = Path.Combine(projectRoot, "ComponentApp.csproj");
        _filePath = Path.Combine(projectRoot, "Components", "Pages", $"Generated.razor");

        var content = GetFileContents(FileType);
        File.WriteAllText(_filePath, content);

        var targetPath = "/Components/Pages/Generated.razor";

        var documentUri = new Uri(_filePath);
        var documentSnapshot = GetDocumentSnapshot(projectFilePath, _filePath, targetPath);
        var documentText = await documentSnapshot.GetTextAsync();
        var documentContext = new VersionedDocumentContext(documentUri, documentSnapshot, projectContext: null, 1);

        _razorRequestContext = new RazorRequestContext(documentContext, Logger, languageServer.GetLspServices());

        _request = new VSInternalDocumentDiagnosticsParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = documentUri
            }
        };
    }

    private object BuildDiagnostics(int numDiagnostics)
    {
        return new RazorPullDiagnosticResponse(
            new[]
            {
                new VSInternalDiagnosticReport()
                {
                    ResultId = "5",
                    Diagnostics = Enumerable.Range(1000, numDiagnostics).Select(x => new Diagnostic
                    {
                        Range = _inRange,
                        Code = "CS" + x,
                        Severity = DiagnosticSeverity.Error,
                        Source = "DocumentPullDiagnosticHandler",
                        Message = "The name 'CallOnMe' does not exist in the current context"
                    }).ToArray()
                }
            }, Array.Empty<VSInternalDiagnosticReport>());
    }

    private protected override LanguageServerFeatureOptions BuildFeatureOptions()
    {
        return Mock.Of<LanguageServerFeatureOptions>(options =>
            options.SupportsFileManipulation == true &&
            options.SupportsDelegatedCodeActions == true &&
            options.SingleServerSupport == true &&
            options.SingleServerCompletionSupport == true &&
            options.CSharpVirtualDocumentSuffix == ".ide.g.cs" &&
            options.HtmlVirtualDocumentSuffix == "__virtual.html",
            MockBehavior.Strict);
    }

    private IRazorDocumentMappingService BuildRazorDocumentMappingService()
    {
        var razorDocumentMappingService = new Mock<IRazorDocumentMappingService>(MockBehavior.Strict);

        Range? hostDocumentRange;
        razorDocumentMappingService.Setup(
            r => r.TryMapToHostDocumentRange(
                It.IsAny<IRazorGeneratedDocument>(),
                _inRange,
                It.IsAny<MappingBehavior>(),
                out hostDocumentRange))
            .Returns((IRazorGeneratedDocument generatedDocument, Range range, MappingBehavior mappingBehavior, out Range? actualOutRange) =>
            {
                actualOutRange = _outRange;
                return true;
            });

        Range? hostDocumentRange2;
        razorDocumentMappingService.Setup(
            r => r.TryMapToHostDocumentRange(
                It.IsAny<IRazorGeneratedDocument>(),
                It.IsNotIn(_inRange),
                It.IsAny<MappingBehavior>(),
                out hostDocumentRange2))
            .Returns((IRazorGeneratedDocument generatedDocument, Range range, MappingBehavior mappingBehavior, out Range? actualOutRange) =>
            {
                actualOutRange = null;
                return false;
            });

        return razorDocumentMappingService.Object;
    }

    private ILoggerFactory BuildLoggerFactory() => Mock.Of<ILoggerFactory>(
        r => r.CreateLogger(
            It.IsAny<string>()) == new NoopLogger(),
        MockBehavior.Strict);

    private string GetFileContents(FileTypes fileType)
    {
        var sb = new StringBuilder();

        sb.Append("""

             <div></div>

             @functions
             {
                public void M()
                {
                    CallOnMe();
                }
             }

             """);

        for (var i = 0; i < (fileType == FileTypes.Small ? 1 : 100); i++)
        {
            sb.Append($$"""
            @{
                var y{{i}} = 456;
            }

            <div>
                <p>Hello there Mr {{i}}</p>
            </div>
            """);
        }

        return sb.ToString();
    }

    [Benchmark(Description = "Diagnostics")]
    public async Task RazorDiagnosticsAsync()
    {
        _diagnostics = await _documentPullDiagnosticsEndpoint!.HandleRequestAsync(_request!, _razorRequestContext, CancellationToken.None);
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (_diagnostics!.Any(x => x!.Diagnostics!.Any(y => !y.Message.Contains("CallOnMe"))))
        {
            throw new NotImplementedException("benchmark setup is wrong");
        }

        File.Delete(_filePath!);

        var innerServer = RazorLanguageServer.GetInnerLanguageServerForTesting();

        await innerServer.ShutdownAsync();
        await innerServer.ExitAsync();
    }

    private class ClientNotifierService : ClientNotifierServiceBase
    {
        private readonly object _diagnostics;

        public ClientNotifierService(object diagnostics)
        {
            _diagnostics = diagnostics;
        }

        public override Task OnInitializedAsync(VSInternalClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override Task SendNotificationAsync<TParams>(string method, TParams @params, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task SendNotificationAsync(string method, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<TResponse> SendRequestAsync<TParams, TResponse>(string method, TParams @params, CancellationToken cancellationToken)
        {
            return Task.FromResult((TResponse)_diagnostics);
        }
    }
}
