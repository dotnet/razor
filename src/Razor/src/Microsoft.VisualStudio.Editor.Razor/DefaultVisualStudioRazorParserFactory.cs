// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [System.Composition.Shared]
    [Export(typeof(VisualStudioRazorParserFactory))]
    internal class DefaultVisualStudioRazorParserFactory : VisualStudioRazorParserFactory
    {
        private readonly ForegroundDispatcher _dispatcher;
        private readonly VisualStudioCompletionBroker _completionBroker;

        [ImportingConstructor]
        public DefaultVisualStudioRazorParserFactory(
            ForegroundDispatcher dispatcher,
            VisualStudioCompletionBroker completionBroker)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            if (completionBroker == null)
            {
                throw new ArgumentNullException(nameof(completionBroker));
            }

            _dispatcher = dispatcher;
            _completionBroker = completionBroker;
        }

        public override VisualStudioRazorParser Create(VisualStudioDocumentTracker documentTracker)
        {
            if (documentTracker == null)
            {
                throw new ArgumentNullException(nameof(documentTracker));
            }

            _dispatcher.AssertForegroundThread();

            var parser = new DefaultVisualStudioRazorParser(
                _dispatcher,
                documentTracker,
                _completionBroker);
            return parser;
        }
    }
}