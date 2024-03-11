// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LegacyEditor.Razor;

public class LegacyRazorTextViewConnectionListenerTest(ITestOutputHelper testOutput) : VisualStudioTestBase(testOutput)
{
    [UIFact]
    public void SubjectBuffersConnected_CallsRazorDocumentManager_OnTextViewOpened()
    {
        // Arrange
        var exportProvider = new StartupInitializerExportProvider();

        var componentModelMock = new StrictMock<IComponentModel>();
        componentModelMock
            .Setup(x => x.DefaultExportProvider)
            .Returns(exportProvider);

        var serviceProviderMock = new StrictMock<IServiceProvider>();
        serviceProviderMock
            .Setup(s => s.GetService(typeof(SComponentModel)))
            .Returns(componentModelMock.Object);

        var textView = StrictMock.Of<ITextView>();
        ITextBuffer[] buffers = [];
        var documentManagerMock = new StrictMock<IRazorDocumentManager>();
        documentManagerMock
            .Setup(d => d.OnTextViewOpenedAsync(textView, buffers))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var listener = new LegacyTextViewConnectionListener(serviceProviderMock.Object, documentManagerMock.Object, JoinableTaskFactory.Context);

        // Act
        listener.SubjectBuffersConnected(textView, ConnectionReason.BufferGraphChange, buffers);

        // Assert
        documentManagerMock.Verify();
    }

    [UIFact]
    public void SubjectBuffersDisconnected_CallsRazorDocumentManager_OnTextViewClosed()
    {
        // Arrange
        var exportProvider = new StartupInitializerExportProvider();

        var componentModelMock = new StrictMock<IComponentModel>();
        componentModelMock
            .Setup(x => x.DefaultExportProvider)
            .Returns(exportProvider);

        var serviceProviderMock = new StrictMock<IServiceProvider>();
        serviceProviderMock
            .Setup(s => s.GetService(typeof(SComponentModel)))
            .Returns(componentModelMock.Object);

        var textView = StrictMock.Of<ITextView>();
        ITextBuffer[] buffers = [];
        var documentManagerMock = new StrictMock<IRazorDocumentManager>();
        documentManagerMock
            .Setup(d => d.OnTextViewClosedAsync(textView, buffers))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var listener = new LegacyTextViewConnectionListener(serviceProviderMock.Object, documentManagerMock.Object, JoinableTaskFactory.Context);

        // Act
        listener.SubjectBuffersDisconnected(textView, ConnectionReason.BufferGraphChange, buffers);

        // Assert
        documentManagerMock.Verify();
    }

    private class StartupInitializerExportProvider : ExportProvider
    {
        private readonly RazorStartupInitializer _startupInitializer = new([]);

        protected override IEnumerable<Export> GetExportsCore(ImportDefinition definition, AtomicComposition atomicComposition)
        {
            yield return new Export("Microsoft.VisualStudio.Editor.Razor.RazorStartupInitializer", () => _startupInitializer);
        }
    }
}
