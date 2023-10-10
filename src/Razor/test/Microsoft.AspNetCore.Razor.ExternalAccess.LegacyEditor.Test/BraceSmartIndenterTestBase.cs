﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Editor.Razor;

public class BraceSmartIndenterTestBase(ITestOutputHelper testOutput) : ProjectSnapshotManagerDispatcherTestBase(testOutput)
{
    private protected static VisualStudioDocumentTracker CreateDocumentTracker(Func<ITextBuffer> bufferAccessor, ITextView focusedTextView)
    {
        var tracker = new Mock<VisualStudioDocumentTracker>(MockBehavior.Strict);
        tracker.Setup(t => t.TextBuffer)
            .Returns(bufferAccessor);
        tracker.Setup(t => t.GetFocusedTextView())
            .Returns(focusedTextView);

        return tracker.Object;
    }

    protected static ITextView CreateFocusedTextView(Func<ITextBuffer>? textBufferAccessor = null, ITextCaret? caret = null)
    {
        var focusedTextView = new Mock<ITextView>(MockBehavior.Strict);
        focusedTextView.Setup(textView => textView.HasAggregateFocus)
            .Returns(true);

        if (textBufferAccessor != null)
        {
            focusedTextView.Setup(textView => textView.TextBuffer)
                .Returns(textBufferAccessor);
        }

        if (caret != null)
        {
            focusedTextView.Setup(textView => textView.Caret)
                .Returns(caret);
        }

        return focusedTextView.Object;
    }

    protected static ITextCaret CreateCaretFrom(int position, ITextSnapshot snapshot)
    {
        var bufferPosition = new VirtualSnapshotPoint(snapshot, position);
        var caret = new Mock<ITextCaret>(MockBehavior.Strict);
        caret.Setup(c => c.Position)
            .Returns(new CaretPosition(bufferPosition, new Mock<IMappingPoint>(MockBehavior.Strict).Object, PositionAffinity.Predecessor));
        caret.Setup(c => c.MoveTo(It.IsAny<SnapshotPoint>()))
            .Returns<SnapshotPoint>(point => new CaretPosition(bufferPosition, new Mock<IMappingPoint>(MockBehavior.Strict).Object, PositionAffinity.Predecessor));

        return caret.Object;
    }

    protected static IEditorOperationsFactoryService CreateOperationsFactoryService()
    {
        var editorOperations = new Mock<IEditorOperations>(MockBehavior.Strict);
        editorOperations.Setup(operations => operations.MoveToEndOfLine(false));
        var editorOperationsFactory = new Mock<IEditorOperationsFactoryService>(MockBehavior.Strict);
        editorOperationsFactory.Setup(factory => factory.GetEditorOperations(It.IsAny<ITextView>()))
            .Returns(editorOperations.Object);

        return editorOperationsFactory.Object;
    }

    private protected static TestTextBuffer CreateTextBuffer(ITextSnapshot initialSnapshot, VisualStudioDocumentTracker documentTracker)
    {
        var textBuffer = new TestTextBuffer(initialSnapshot);
        textBuffer.Properties.AddProperty(typeof(VisualStudioDocumentTracker), documentTracker);

        return textBuffer;
    }
}
