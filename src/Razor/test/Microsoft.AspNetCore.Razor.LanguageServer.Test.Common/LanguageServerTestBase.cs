﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Newtonsoft.Json;

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
            SpanMappingService = new ThrowingRazorSpanMappingService();
            var logger = new Mock<ILogger>(MockBehavior.Strict).Object;
            Mock.Get(logger).Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>())).Verifiable();
            Mock.Get(logger).Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(false);

            LoggerFactory = TestLoggerFactory.Instance;
            LspLogger = TestLspLogger.Instance;
            Logger = TestLspLogger.Instance;

            Serializer = new JsonSerializer();
            Serializer.Converters.RegisterRazorConverters();
            Serializer.AddVSInternalExtensionConverters();
            Serializer.AddVSExtensionConverters();
        }

        // This is marked as legacy because in its current form it's being assigned a "TestProjectSnapshotManagerDispatcher" which takes the
        // synchronization context from the constructing thread and binds to that. We've seen in XUnit how this can unexpectedly lead to flaky
        // tests since it doesn't actually replicate what happens in real scenario (a separate dedicated dispatcher thread). If you're reading
        // this write your tests using the normal Dispatcher property. Eventually this LegacyDispatcher property will go away when we've had
        // the opportunity to re-write our tests correctly.
        internal ProjectSnapshotManagerDispatcher LegacyDispatcher { get; }

        internal readonly ProjectSnapshotManagerDispatcher Dispatcher = new LSPProjectSnapshotManagerDispatcher(TestLoggerFactory.Instance);

        internal FilePathNormalizer FilePathNormalizer { get; }

        protected JsonSerializer Serializer { get; }

        internal IRazorSpanMappingService SpanMappingService { get; }

        protected ILspLogger LspLogger { get; } = TestLspLogger.Instance;

        protected ILogger Logger { get; } = TestLspLogger.Instance;

        protected ILoggerFactory LoggerFactory { get; }

        internal static RazorRequestContext CreateRazorRequestContext(DocumentContext? documentContext, ILspLogger? lspLogger = null, ILogger? logger = null, ILspServices? lspServices = null)
        {
            lspLogger ??= TestLspLogger.Instance;
            logger ??= TestLspLogger.Instance;
            lspServices ??= new Mock<ILspServices>(MockBehavior.Strict).Object;

            var requestContext = new RazorRequestContext(documentContext, lspLogger, logger, lspServices);

            return requestContext;
        }

        protected static RazorCodeDocument CreateCodeDocument(string text, IReadOnlyList<TagHelperDescriptor>? tagHelpers = null)
        {
            tagHelpers ??= Array.Empty<TagHelperDescriptor>();
            var sourceDocument = TestRazorSourceDocument.Create(text);
            var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, RazorProjectFileSystem.Create("C:/"), builder =>
            {
                RazorExtensions.Register(builder);
            });
            var defaultImportDocument = TestRazorSourceDocument.Create(
                """
                @using System;
                """,
                new RazorSourceDocumentProperties("_ViewImports.cshtml", "_ViewImports.cshtml"));
            var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, "mvc", new[] { defaultImportDocument }, tagHelpers);
            return codeDocument;
        }

        internal static DocumentContextFactory CreateDocumentContextFactory(Uri documentPath, string sourceText)
        {
            var codeDocument = CreateCodeDocument(sourceText);
            return CreateDocumentContextFactory(documentPath, codeDocument);
        }

        internal static DocumentContext? CreateDocumentContext(Uri documentPath, RazorCodeDocument codeDocument, [NotNullWhen(true)] bool documentFound = true)
        {
            return documentFound ? TestDocumentContext.From(documentPath.GetAbsoluteOrUNCPath(), codeDocument, hostDocumentVersion: 1337) : null;
        }

        internal static string GetString(SourceText sourceText)
        {
            var sourceChars = new char[sourceText.Length];
            sourceText.CopyTo(0, sourceChars, 0, sourceText.Length);
            var sourceString = new string(sourceChars);

            return sourceString;
        }

        internal static DocumentContextFactory CreateDocumentContextFactory(
            Uri documentPath,
            RazorCodeDocument codeDocument,
            bool documentFound = true)
        {
            var documentContextFactory = documentFound
                ? new TestDocumentContextFactory(documentPath.GetAbsoluteOrUNCPath(), codeDocument, version: 1337)
                : new TestDocumentContextFactory();
            return documentContextFactory;
        }

        internal static DocumentContext CreateDocumentContext(Uri uri, DocumentSnapshot snapshot)
        {
            return new DocumentContext(uri, snapshot, version: 0);
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

        private class ThrowingRazorSpanMappingService : IRazorSpanMappingService
        {
            public Task<ImmutableArray<RazorMappedSpanResult>> MapSpansAsync(Document document, IEnumerable<TextSpan> spans, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
