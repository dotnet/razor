// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;

namespace Microsoft.AspNetCore.Razor.Test.Common
{
    public abstract class LanguageServerTestBase
    {
        public LanguageServerTestBase()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            LegacyDispatcher = new TestProjectSnapshotManagerDispatcher();
#pragma warning restore CS0618 // Type or member is obsolete
            FilePathNormalizer = new FilePathNormalizer();
            var logger = new Mock<ILogger>(MockBehavior.Strict).Object;
            Mock.Get(logger).Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>())).Verifiable();
            Mock.Get(logger).Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(false);
            LoggerFactory = Mock.Of<ILoggerFactory>(factory => factory.CreateLogger(It.IsAny<string>()) == logger, MockBehavior.Strict);
            Dispatcher = new LSPProjectSnapshotManagerDispatcher(LoggerFactory);
        }

        // This is marked as legacy because in its current form it's being assigned a "TestProjectSnapshotManagerDispatcher" which takes the
        // synchronization context from the constructing thread and binds to that. We've seen in XUnit how this can unexpectedly lead to flaky
        // tests since it doesn't actually replicate what happens in real scenario (a separate dedicated dispatcher thread). If you're reading
        // this write your tests using the normal Dispatcher property. Eventually this LegacyDispatcher property will go away when we've had
        // the opportunity to re-write our tests correctly.
        internal ProjectSnapshotManagerDispatcher LegacyDispatcher { get; }

        internal ProjectSnapshotManagerDispatcher Dispatcher { get; }

        internal FilePathNormalizer FilePathNormalizer { get; }

        protected ILoggerFactory LoggerFactory { get; }

        protected static AdhocWorkspace CreateTestWorkspace(IEnumerable<(Uri documentUri, SourceText csharpSourceText)> files)
        {
            var workspace = TestWorkspace.Create() as AdhocWorkspace;

            // Add project and solution to workspace
            var projectInfo = ProjectInfo.Create(
                id: ProjectId.CreateNewId("TestProject"),
                version: VersionStamp.Default,
                name: "TestProject",
                assemblyName: "TestProject",
                language: LanguageNames.CSharp,
                filePath: "C:\\TestSolution\\TestProject.csproj");

            var solutionInfo = SolutionInfo.Create(
                id: SolutionId.CreateNewId("TestSolution"),
                version: VersionStamp.Default,
                projects: new ProjectInfo[] { projectInfo });

            workspace.AddSolution(solutionInfo);

            // Add document to workspace. We use an IVT method to create the DocumentInfo variable because there's a special constructor in Roslyn that will
            // help identify the document as belonging to Razor.
            var exportProvider = TestCompositions.Roslyn.ExportProviderFactory.CreateExportProvider();
            var languageServerFactory = exportProvider.GetExportedValue<IRazorLanguageServerFactoryWrapper>();

            var documentCount = 0;
            foreach (var (documentUri, csharpSourceText) in files)
            {
                var documentFilePath = documentUri.AbsolutePath;
                var textAndVersion = TextAndVersion.Create(csharpSourceText, VersionStamp.Default, documentFilePath);
                var documentInfo = languageServerFactory.CreateDocumentInfo(
                    id: DocumentId.CreateNewId(projectInfo.Id),
                    name: "TestDocument" + documentCount,
                    filePath: documentFilePath,
                    loader: TextLoader.From(textAndVersion),
                    razorDocumentServiceProvider: new TestRazorDocumentServiceProvider());

                workspace.AddDocument(documentInfo);
                documentCount++;
            }

            return workspace;
        }

        protected static async Task<CSharpTestLspServer> CreateCSharpLspServerAsync(AdhocWorkspace workspace, ServerCapabilities serverCapabilities)
        {
            var clientCapabilities = new ClientCapabilities();
            var testLspServer = await CSharpTestLspServer.CreateAsync(workspace, clientCapabilities, serverCapabilities).ConfigureAwait(false);
            return testLspServer;
        }

        private class TestRazorDocumentServiceProvider : IRazorDocumentServiceProvider
        {
            public bool CanApplyChange => throw new NotImplementedException();

            public bool SupportDiagnostics => throw new NotImplementedException();

            TService IRazorDocumentServiceProvider.GetService<TService>()
            {
                var serviceType = typeof(TService);

                if (serviceType == typeof(IRazorSpanMappingService))
                {
                    return (TService)(IRazorSpanMappingService)new TestRazorSpanMappingService();
                }

                return this as TService;
            }

            private class TestRazorSpanMappingService : IRazorSpanMappingService
            {
                public Task<ImmutableArray<RazorMappedSpanResult>> MapSpansAsync(
                    Document document,
                    IEnumerable<TextSpan> spans,
                    CancellationToken cancellationToken)
                {
                    throw new NotImplementedException();
                }
            }
        }

        [Obsolete("Use " + nameof(LSPProjectSnapshotManagerDispatcher))]
        private class TestProjectSnapshotManagerDispatcher : ProjectSnapshotManagerDispatcher
        {
            public TestProjectSnapshotManagerDispatcher()
            {
                DispatcherScheduler = SynchronizationContext.Current is null
                    ? new ThrowingTaskScheduler()
                    : TaskScheduler.FromCurrentSynchronizationContext();
            }

            public override TaskScheduler DispatcherScheduler { get; }

            private Thread Thread { get; } = Thread.CurrentThread;

            public override bool IsDispatcherThread => Thread.CurrentThread == Thread;
        }

        private class ThrowingTaskScheduler : TaskScheduler
        {
            protected override IEnumerable<Task> GetScheduledTasks()
            {
                return Enumerable.Empty<Task>();
            }

            protected override void QueueTask(Task task)
            {
                throw new NotImplementedException();
            }

            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                throw new NotImplementedException();
            }
        }
    }
}
