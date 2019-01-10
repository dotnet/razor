// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Editor
{
    [System.Composition.Shared]
    [Export(typeof(VisualStudioCompletionBroker))]
    internal class DefaultVisualStudioCompletionBroker : VisualStudioCompletionBroker
    {
        private readonly ICompletionBroker _completionBroker;

        [ImportingConstructor]
        public DefaultVisualStudioCompletionBroker(ICompletionBroker completionBroker)
        {
            if (completionBroker == null)
            {
                throw new ArgumentNullException(nameof(completionBroker));
            }

            _completionBroker = completionBroker;
        }

        public override bool IsCompletionActive(ITextView textView)
        {
            if (textView == null)
            {
                throw new ArgumentNullException(nameof(textView));
            }

            var completionIsActive = _completionBroker.IsCompletionActive(textView);
            return completionIsActive;
        }
    }
}
