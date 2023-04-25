// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.LanguageServer;

public class RazorSemanticTokensRangeEndpointBenchmark : RazorLanguageServerBenchmarkBase
{
    private IRazorSemanticTokensInfoService RazorSemanticTokenService { get; set; }

    private SemanticTokensRangeEndpoint SemanticTokensRangeEndpoint { get; set; }

    private DocumentVersionCache VersionCache { get; set; }

    private Uri DocumentUri => DocumentContext.Uri;

    private IDocumentSnapshot DocumentSnapshot => DocumentContext.Snapshot;

    private VersionedDocumentContext DocumentContext { get; set; }

    private Range Range { get; set; }

    private new IRazorLogger Logger { get; set; }

    private ProjectSnapshotManagerDispatcher ProjectSnapshotManagerDispatcher { get; set; }

    private string PagesDirectory { get; set; }

    private string ProjectFilePath { get; set; }

    private string TargetPath { get; set; }

    private static int NumberOfCsSemanticRangesToReturn { get; set; }

    [GlobalSetup]
    public async Task InitializeRazorSemanticAsync()
    {
        EnsureServicesInitialized();

        var projectRoot = Path.Combine(RepoRoot, "src", "Razor", "test", "testapps", "ComponentApp");
        ProjectFilePath = Path.Combine(projectRoot, "ComponentApp.csproj");
        PagesDirectory = Path.Combine(projectRoot, "Components", "Pages");
        var filePath = Path.Combine(PagesDirectory, $"SemanticTokens.razor");
        TargetPath = "/Components/Pages/SemanticTokens.razor";

        var documentUri = new Uri(filePath);
        var documentSnapshot = GetDocumentSnapshot(ProjectFilePath, filePath, TargetPath);
        var version = 1;
        DocumentContext = new VersionedDocumentContext(documentUri, documentSnapshot, version);
        Logger = new NoopLogger();
        SemanticTokensRangeEndpoint = new SemanticTokensRangeEndpoint();

        var text = await DocumentContext.GetSourceTextAsync(CancellationToken.None).ConfigureAwait(false);
        Range = new Range
        {
            Start = new Position { Line = 0, Character = 0 },
            End = new Position { Line = text.Lines.Count - 1, Character = text.Lines.Last().Span.Length - 1 }
        };
    }

    [Benchmark(Description = "Razor Semantic Tokens Range Endpoint [0]")]
    public async Task RazorSemanticTokensRangeEndpointZeroCsRangesAsync()
    {
        var textDocumentIdentifier = new TextDocumentIdentifier { Uri = DocumentUri };
        var cancellationToken = CancellationToken.None;
        var documentVersion = 1;

        await UpdateDocumentAsync(documentVersion, DocumentSnapshot, cancellationToken).ConfigureAwait(false);

        var request = new SemanticTokensRangeParams { Range = Range, TextDocument = textDocumentIdentifier };

        var languageServer = RazorLanguageServer.GetInnerLanguageServerForTesting();
        var requestContext = new RazorRequestContext(DocumentContext, Logger, languageServer.GetLspServices());

        NumberOfCsSemanticRangesToReturn = 0;
        await SemanticTokensRangeEndpoint.HandleRequestAsync(request, requestContext, cancellationToken);
    }

    [Benchmark(Description = "Razor Semantic Tokens Range Endpoint [100]")]
    public async Task RazorSemanticTokensRangeEndpoint100CsRangesAsync()
    {
        var textDocumentIdentifier = new TextDocumentIdentifier { Uri = DocumentUri };
        var cancellationToken = CancellationToken.None;
        var documentVersion = 1;

        await UpdateDocumentAsync(documentVersion, DocumentSnapshot, cancellationToken).ConfigureAwait(false);

        var request = new SemanticTokensRangeParams { Range = Range, TextDocument = textDocumentIdentifier };

        var languageServer = RazorLanguageServer.GetInnerLanguageServerForTesting();
        var requestContext = new RazorRequestContext(DocumentContext, Logger, languageServer.GetLspServices());

        NumberOfCsSemanticRangesToReturn = 100;
        await SemanticTokensRangeEndpoint.HandleRequestAsync(request, requestContext, cancellationToken);
    }

    [Benchmark(Description = "Razor Semantic Tokens Range Endpoint [1000]")]
    public async Task RazorSemanticTokensRangeEndpoint1000CsRangesAsync()
    {
        var textDocumentIdentifier = new TextDocumentIdentifier { Uri = DocumentUri };
        var cancellationToken = CancellationToken.None;
        var documentVersion = 1;

        await UpdateDocumentAsync(documentVersion, DocumentSnapshot, cancellationToken).ConfigureAwait(false);

        var request = new SemanticTokensRangeParams { Range = Range, TextDocument = textDocumentIdentifier };

        var languageServer = RazorLanguageServer.GetInnerLanguageServerForTesting();
        var requestContext = new RazorRequestContext(DocumentContext, Logger, languageServer.GetLspServices());

        NumberOfCsSemanticRangesToReturn = 100;
        await SemanticTokensRangeEndpoint.HandleRequestAsync(request, requestContext, cancellationToken);
    }

    private async Task UpdateDocumentAsync(int newVersion, IDocumentSnapshot documentSnapshot,
        CancellationToken cancellationToken)
    {
        await ProjectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => VersionCache.TrackDocumentVersion(documentSnapshot, newVersion), cancellationToken)
            .ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async Task CleanupServerAsync()
    {
        var innerServer = RazorLanguageServer.GetInnerLanguageServerForTesting();
        await innerServer.ShutdownAsync();
        await innerServer.ExitAsync();
    }

    protected internal override void Builder(IServiceCollection collection)
    {
        collection.AddSingleton<IRazorSemanticTokensInfoService, TestCustomizableRazorSemanticTokensInfoService>();
    }

    private void EnsureServicesInitialized()
    {
        var languageServer = RazorLanguageServer.GetInnerLanguageServerForTesting();
        RazorSemanticTokenService = languageServer.GetRequiredService<IRazorSemanticTokensInfoService>();
        VersionCache = languageServer.GetRequiredService<DocumentVersionCache>();
        ProjectSnapshotManagerDispatcher = languageServer.GetRequiredService<ProjectSnapshotManagerDispatcher>();
    }

    internal class TestCustomizableRazorSemanticTokensInfoService : RazorSemanticTokensInfoService
    {
        private readonly Random _random;

        public TestCustomizableRazorSemanticTokensInfoService(
            ClientNotifierServiceBase languageServer,
            RazorDocumentMappingService documentMappingService,
            ILoggerFactory loggerFactory)
            : base(languageServer, documentMappingService, loggerFactory)
        {
            _random = new Random();
        }

        // We can't get C# responses without significant amounts of extra work, so let's just shim it for now, any non-Null result is fine.
        internal override Task<List<SemanticRange>> GetCSharpSemanticRangesAsync(
            RazorCodeDocument codeDocument,
            TextDocumentIdentifier textDocumentIdentifier,
            Range razorRange,
            long documentVersion,
            CancellationToken cancellationToken,
            string previousResultId = null)
        {
            var ranges = new List<SemanticRange>();
            for (var i = 0; i < NumberOfCsSemanticRangesToReturn; i++)
            {
                var startLine = _random.Next(razorRange.Start.Line, razorRange.End.Line);
                var startChar = _random.Next(0, codeDocument.Source.Lines.GetLineLength(startLine));
                var endLine = _random.Next(startLine, razorRange.End.Line);
                var endChar = startLine == endLine
                    ? _random.Next(startChar, codeDocument.Source.Lines.GetLineLength(startLine))
                    : _random.Next(0, codeDocument.Source.Lines.GetLineLength(endLine));

                ranges.Add(
                    new SemanticRange(_random.Next(),
                        new Range { Start = new Position(startLine, startChar), End = new Position(endLine, endChar) },
                        0));
            }

            return Task.FromResult(ranges);
        }
    }
}
