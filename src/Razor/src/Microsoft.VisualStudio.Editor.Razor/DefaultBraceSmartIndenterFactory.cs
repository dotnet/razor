// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor
{
    internal class DefaultBraceSmartIndenterFactory : BraceSmartIndenterFactory
    {
        private readonly IEditorOperationsFactoryService _editorOperationsFactory;
        private ForegroundDispatcher _foregroundDispatcher;
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly TextBufferCodeDocumentProvider _codeDocumentProvider;

        public DefaultBraceSmartIndenterFactory(
            ForegroundDispatcher foregroundDispatcher,
            JoinableTaskContext joinableTaskContext,
            TextBufferCodeDocumentProvider codeDocumentProvider,
            IEditorOperationsFactoryService editorOperationsFactory)
        {
            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            if (codeDocumentProvider is null)
            {
                throw new ArgumentNullException(nameof(codeDocumentProvider));
            }

            if (editorOperationsFactory is null)
            {
                throw new ArgumentNullException(nameof(editorOperationsFactory));
            }
            _foregroundDispatcher = foregroundDispatcher;
            _joinableTaskContext = joinableTaskContext;
            _codeDocumentProvider = codeDocumentProvider;
            _editorOperationsFactory = editorOperationsFactory;
        }

        public override BraceSmartIndenter Create(VisualStudioDocumentTracker documentTracker)
        {
            if (documentTracker == null)
            {
                throw new ArgumentNullException(nameof(documentTracker));
            }

            _joinableTaskContext.AssertUIThread();

            var braceSmartIndenter = new BraceSmartIndenter(_foregroundDispatcher, _joinableTaskContext, documentTracker, _codeDocumentProvider, _editorOperationsFactory);

            return braceSmartIndenter;
        }
    }
}
