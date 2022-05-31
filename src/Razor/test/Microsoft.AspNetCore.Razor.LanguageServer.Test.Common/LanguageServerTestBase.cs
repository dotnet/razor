// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Logging;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

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
            Serializer = new LspSerializer();
            Serializer.RegisterRazorConverters();
            Serializer.RegisterVSInternalExtensionConverters();
        }

        // This is marked as legacy because in its current form it's being assigned a "TestProjectSnapshotManagerDispatcher" which takes the
        // synchronization context from the constructing thread and binds to that. We've seen in XUnit how this can unexpectedly lead to flaky
        // tests since it doesn't actually replicate what happens in real scenario (a separate dedicated dispatcher thread). If you're reading
        // this write your tests using the normal Dispatcher property. Eventually this LegacyDispatcher property will go away when we've had
        // the opportunity to re-write our tests correctly.
        internal ProjectSnapshotManagerDispatcher LegacyDispatcher { get; }

        internal ProjectSnapshotManagerDispatcher Dispatcher { get; }

        internal FilePathNormalizer FilePathNormalizer { get; }

        protected LspSerializer Serializer { get; }

        protected ILoggerFactory LoggerFactory { get; }

        protected static RazorCodeDocument CreateCodeDocument(string text, IReadOnlyList<TagHelperDescriptor> tagHelpers = null)
        {
            tagHelpers ??= Array.Empty<TagHelperDescriptor>();
            var sourceDocument = TestRazorSourceDocument.Create(text);
            var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, RazorProjectFileSystem.Create("/"), builder => { });
            var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, "mvc", Array.Empty<RazorSourceDocument>(), tagHelpers);
            return codeDocument;
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
