// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Editor.Razor;

[Export(typeof(RazorEditorFactoryService))]
[method: ImportingConstructor]
internal class DefaultRazorEditorFactoryService(
    IVisualStudioDocumentTrackerFactory documentTrackerFactory,
    VisualStudioWorkspaceAccessor workspaceAccessor) : RazorEditorFactoryService
{
    private static readonly object s_razorTextBufferInitializationKey = new();

    private readonly IVisualStudioDocumentTrackerFactory _documentTrackerFactory = documentTrackerFactory;
    private readonly VisualStudioWorkspaceAccessor _workspaceAccessor = workspaceAccessor;

    public override bool TryGetDocumentTracker(ITextBuffer textBuffer, [NotNullWhen(true)] out IVisualStudioDocumentTracker? documentTracker)
    {
        if (textBuffer is null)
        {
            throw new ArgumentNullException(nameof(textBuffer));
        }

        if (!textBuffer.IsLegacyCoreRazorBuffer())
        {
            documentTracker = null;
            return false;
        }

        var textBufferInitialized = TryInitializeTextBuffer(textBuffer);
        if (!textBufferInitialized)
        {
            documentTracker = null;
            return false;
        }

        if (!textBuffer.Properties.TryGetProperty(typeof(IVisualStudioDocumentTracker), out documentTracker) ||
            documentTracker is null)
        {
            Debug.Fail("Document tracker should have been stored on the text buffer during initialization.");
            return false;
        }

        return true;
    }

    public override bool TryGetParser(ITextBuffer textBuffer, [NotNullWhen(true)] out VisualStudioRazorParser? parser)
    {
        if (textBuffer is null)
        {
            throw new ArgumentNullException(nameof(textBuffer));
        }

        if (!textBuffer.IsLegacyCoreRazorBuffer())
        {
            parser = null;
            return false;
        }

        var textBufferInitialized = TryInitializeTextBuffer(textBuffer);
        if (!textBufferInitialized)
        {
            parser = null;
            return false;
        }

        if (!textBuffer.Properties.TryGetProperty(typeof(VisualStudioRazorParser), out parser) ||
            parser is null)
        {
            Debug.Fail("Parser should have been stored on the text buffer during initialization.");
            return false;
        }

        return true;
    }

    internal override bool TryGetSmartIndenter(ITextBuffer textBuffer, [NotNullWhen(returnValue: true)] out BraceSmartIndenter? braceSmartIndenter)
    {
        if (textBuffer is null)
        {
            throw new ArgumentNullException(nameof(textBuffer));
        }

        if (!textBuffer.IsLegacyCoreRazorBuffer())
        {
            braceSmartIndenter = null;
            return false;
        }

        var textBufferInitialized = TryInitializeTextBuffer(textBuffer);
        if (!textBufferInitialized)
        {
            braceSmartIndenter = null;
            return false;
        }

        if (!textBuffer.Properties.TryGetProperty(typeof(BraceSmartIndenter), out braceSmartIndenter!))
        {
            Debug.Fail("Brace smart indenter should have been stored on the text buffer during initialization.");
            return false;
        }

        return true;
    }

    // Internal for testing
    internal bool TryInitializeTextBuffer(ITextBuffer textBuffer)
    {
        if (textBuffer.Properties.ContainsProperty(s_razorTextBufferInitializationKey))
        {
            // Buffer already initialized.
            return true;
        }

        if (!_workspaceAccessor.TryGetWorkspace(textBuffer, out var workspace))
        {
            // Could not locate workspace for given text buffer.
            return false;
        }

        var razorLanguageServices = workspace.Services.GetLanguageServices(RazorLanguage.Name);
        var parserFactory = razorLanguageServices.GetRequiredService<VisualStudioRazorParserFactory>();
        var braceSmartIndenterFactory = razorLanguageServices.GetRequiredService<BraceSmartIndenterFactory>();

        var tracker = _documentTrackerFactory.Create(textBuffer);
        Assumes.NotNull(tracker);
        textBuffer.Properties[typeof(IVisualStudioDocumentTracker)] = tracker;

        var parser = parserFactory.Create(tracker);
        textBuffer.Properties[typeof(VisualStudioRazorParser)] = parser;

        var braceSmartIndenter = braceSmartIndenterFactory.Create(tracker);
        textBuffer.Properties[typeof(BraceSmartIndenter)] = braceSmartIndenter;

        textBuffer.Properties.AddProperty(s_razorTextBufferInitializationKey, s_razorTextBufferInitializationKey);

        return true;
    }
}
