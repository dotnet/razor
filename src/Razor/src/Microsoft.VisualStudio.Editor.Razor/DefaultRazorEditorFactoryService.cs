// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [System.Composition.Shared]
    [Export(typeof(RazorEditorFactoryService))]
    internal class DefaultRazorEditorFactoryService : RazorEditorFactoryService
    {
        private static readonly object s_razorTextBufferInitializationKey = new object();
        private readonly VisualStudioWorkspaceAccessor _workspaceAccessor;

        [ImportingConstructor]
        public DefaultRazorEditorFactoryService(VisualStudioWorkspaceAccessor workspaceAccessor)
        {
            if (workspaceAccessor is null)
            {
                throw new ArgumentNullException(nameof(workspaceAccessor));
            }

            _workspaceAccessor = workspaceAccessor;
        }

        public override bool TryGetDocumentTracker(ITextBuffer textBuffer, out VisualStudioDocumentTracker documentTracker)
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

            if (!textBuffer.Properties.TryGetProperty(typeof(VisualStudioDocumentTracker), out documentTracker))
            {
                Debug.Fail("Document tracker should have been stored on the text buffer during initialization.");
                return false;
            }

            return true;
        }

        public override bool TryGetParser(ITextBuffer textBuffer, out VisualStudioRazorParser parser)
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

            if (!textBuffer.Properties.TryGetProperty(typeof(VisualStudioRazorParser), out parser))
            {
                Debug.Fail("Parser should have been stored on the text buffer during initialization.");
                return false;
            }

            return true;
        }

        internal override bool TryGetSmartIndenter(ITextBuffer textBuffer, out BraceSmartIndenter braceSmartIndenter)
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

            if (!textBuffer.Properties.TryGetProperty(typeof(BraceSmartIndenter), out braceSmartIndenter))
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
            var documentTrackerFactory = razorLanguageServices.GetRequiredService<VisualStudioDocumentTrackerFactory>();
            var parserFactory = razorLanguageServices.GetRequiredService<VisualStudioRazorParserFactory>();
            var braceSmartIndenterFactory = razorLanguageServices.GetRequiredService<BraceSmartIndenterFactory>();

            var tracker = documentTrackerFactory.Create(textBuffer);
            textBuffer.Properties[typeof(VisualStudioDocumentTracker)] = tracker;

            var parser = parserFactory.Create(tracker);
            textBuffer.Properties[typeof(VisualStudioRazorParser)] = parser;

            var braceSmartIndenter = braceSmartIndenterFactory.Create(tracker);
            textBuffer.Properties[typeof(BraceSmartIndenter)] = braceSmartIndenter;

            textBuffer.Properties.AddProperty(s_razorTextBufferInitializationKey, s_razorTextBufferInitializationKey);

            return true;
        }
    }
}
