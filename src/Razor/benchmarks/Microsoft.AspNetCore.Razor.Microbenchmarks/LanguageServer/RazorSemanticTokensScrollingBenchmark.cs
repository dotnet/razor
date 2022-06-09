﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using static Microsoft.AspNetCore.Razor.Microbenchmarks.LanguageServer.RazorSemanticTokensBenchmark;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.LanguageServer
{
    public class RazorSemanticTokensScrollingBenchmark : RazorLanguageServerBenchmarkBase
    {
        private RazorLanguageServer RazorLanguageServer { get; set; }

        private DefaultRazorSemanticTokensInfoService RazorSemanticTokenService { get; set; }

        private DocumentVersionCache VersionCache { get; set; }

        private DocumentContext DocumentContext { get; set; }

        private Uri DocumentUri => DocumentContext.Uri;

        private DocumentSnapshot DocumentSnapshot => DocumentContext.Snapshot;

        private Range Range { get; set; }

        private ProjectSnapshotManagerDispatcher ProjectSnapshotManagerDispatcher { get; set; }

        private string PagesDirectory { get; set; }

        private string ProjectFilePath { get; set; }

        private string TargetPath { get; set; }

        [GlobalSetup(Target = nameof(RazorSemanticTokensRangeScrollingAsync))]
        public async Task InitializeRazorSemanticAsync()
        {
            await EnsureServicesInitializedAsync();

            var projectRoot = Path.Combine(RepoRoot, "src", "Razor", "test", "testapps", "ComponentApp");
            ProjectFilePath = Path.Combine(projectRoot, "ComponentApp.csproj");
            PagesDirectory = Path.Combine(projectRoot, "Components", "Pages");
            var filePath = Path.Combine(PagesDirectory, $"FormattingTest.razor");
            TargetPath = "/Components/Pages/FormattingTest.razor";

            var documentUri = new Uri(filePath);
            var documentSnapshot = GetDocumentSnapshot(ProjectFilePath, filePath, TargetPath);
            DocumentContext = new DocumentContext(documentUri, documentSnapshot, version: 1);

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
                    Character = 0
                }
            };
        }

        private const int WindowSize = 10;

        [Benchmark(Description = "Razor Semantic Tokens Range Scrolling")]
        public async Task RazorSemanticTokensRangeScrollingAsync()
        {
            var textDocumentIdentifier = new TextDocumentIdentifier()
            {
                Uri = DocumentUri
            };
            var cancellationToken = CancellationToken.None;
            var documentVersion = 1;

            await UpdateDocumentAsync(documentVersion, DocumentSnapshot).ConfigureAwait(false);

            var documentLineCount = Range.End.Line;

            var lineCount = 0;
            while (lineCount != documentLineCount)
            {
                var newLineCount = Math.Min(lineCount + WindowSize, documentLineCount);
                var range = new Range
                {
                    Start = new Position(lineCount, 0),
                    End = new Position(newLineCount, 0)
                };
                await RazorSemanticTokenService!.GetSemanticTokensAsync(
                    textDocumentIdentifier,
                    range,
                    DocumentContext,
                    cancellationToken);

                lineCount = newLineCount;
            }
        }

        private async Task UpdateDocumentAsync(int newVersion, DocumentSnapshot documentSnapshot)
        {
            await ProjectSnapshotManagerDispatcher!.RunOnDispatcherThreadAsync(
                () => VersionCache!.TrackDocumentVersion(documentSnapshot, newVersion), CancellationToken.None).ConfigureAwait(false);
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
    }
}
