// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.LanguageServer
{
    public class RazorSemanticTokensBenchmark : RazorLanguageServerBenchmarkBase
    {
        private RazorLanguageServer RazorLanguageServer { get; set; }

        private DefaultRazorSemanticTokensInfoService RazorSemanticTokenService { get; set; }

        private DocumentVersionCache VersionCache { get; set; }

        private DocumentUri DocumentUri { get; set; }

        private DocumentSnapshot DocumentSnapshot { get; set; }

        private Range Range { get; set; }

        private ProjectSnapshotManagerDispatcher ProjectSnapshotManagerDispatcher { get; set; }

        private string PagesDirectory { get; set; }

        private string ProjectFilePath { get; set; }

        private string TargetPath { get; set; }

        [GlobalSetup(Target = nameof(RazorSemanticTokensRangeAsync))]
        public async Task InitializeRazorSemanticAsync()
        {
            await EnsureServicesInitializedAsync();

            var projectRoot = Path.Combine(RepoRoot, "src", "Razor", "test", "testapps", "ComponentApp");
            ProjectFilePath = Path.Combine(projectRoot, "ComponentApp.csproj");
            PagesDirectory = Path.Combine(projectRoot, "Components", "Pages");
            var filePath = Path.Combine(PagesDirectory, $"SemanticTokens.razor");
            TargetPath = "/Components/Pages/SemanticTokens.razor";

            DocumentUri = DocumentUri.File(filePath);
            DocumentSnapshot = GetDocumentSnapshot(ProjectFilePath, filePath, TargetPath);

            var text = await DocumentSnapshot.GetTextAsync().ConfigureAwait(false);
            Range = new Range
            {
                Start = new Position
                {
                    Line = 0,
                    Character = 0
                },
                End = new Position
                {
                    Line = text.Lines.Count - 1,
                    Character = text.Lines.Last().Span.Length - 1
                }
            };
        }

        [Benchmark(Description = "Razor Semantic Tokens Range Handling")]
        public async Task RazorSemanticTokensRangeAsync()
        {
            var textDocumentIdentifier = new TextDocumentIdentifier(DocumentUri);
            var cancellationToken = CancellationToken.None;
            var documentVersion = 1;

            await UpdateDocumentAsync(documentVersion, DocumentSnapshot).ConfigureAwait(false);
            await RazorSemanticTokenService.GetSemanticTokensAsync(
                textDocumentIdentifier, Range, DocumentSnapshot, documentVersion, cancellationToken).ConfigureAwait(false);
        }

        private async Task UpdateDocumentAsync(int newVersion, DocumentSnapshot documentSnapshot)
        {
            await ProjectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => VersionCache.TrackDocumentVersion(documentSnapshot, newVersion), CancellationToken.None).ConfigureAwait(false);
        }

        [GlobalCleanup]
        public void CleanupServer()
        {
            RazorLanguageServer?.Dispose();
        }

        protected internal override void Builder(RazorLanguageServerBuilder builder)
        {
            builder.Services.AddSingleton<RazorSemanticTokensInfoService, TestRazorSemanticTokensInfoService>();
        }

        private async Task EnsureServicesInitializedAsync()
        {
            if (RazorLanguageServer != null)
            {
                return;
            }

            RazorLanguageServer = await RazorLanguageServerTask.ConfigureAwait(false);
            var languageServer = RazorLanguageServer.GetInnerLanguageServerForTesting();
            RazorSemanticTokenService = languageServer.GetService(typeof(RazorSemanticTokensInfoService)) as TestRazorSemanticTokensInfoService;
            VersionCache = languageServer.GetService(typeof(DocumentVersionCache)) as DocumentVersionCache;
            ProjectSnapshotManagerDispatcher = languageServer.GetService(typeof(ProjectSnapshotManagerDispatcher)) as ProjectSnapshotManagerDispatcher;
        }

        internal class TestRazorSemanticTokensInfoService : DefaultRazorSemanticTokensInfoService
        {
            public TestRazorSemanticTokensInfoService(
                ClientNotifierServiceBase languageServer,
                RazorDocumentMappingService documentMappingService,
                ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
                DocumentResolver documentResolver,
                DocumentVersionCache documentVersionCache,
                LoggerFactory loggerFactory) :
                base(languageServer, documentMappingService, projectSnapshotManagerDispatcher, documentResolver, documentVersionCache, loggerFactory)
            {
            }

            // We can't get C# responses without significant amounts of extra work, so let's just shim it for now, any non-Null result is fine.
            internal override Task<SemanticRange[]> GetCSharpSemanticRangesAsync(
                RazorCodeDocument codeDocument,
                TextDocumentIdentifier textDocumentIdentifier,
                Range range,
                long documentVersion,
                CancellationToken cancellationToken,
                string previousResultId = null)
            {
                var result = Array.Empty<SemanticRange>();
                return Task.FromResult(result);
            }
        }
    }
}
