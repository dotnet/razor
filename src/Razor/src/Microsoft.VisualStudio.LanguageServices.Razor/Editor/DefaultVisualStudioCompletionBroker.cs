// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Editor
{
    internal class DefaultVisualStudioCompletionBroker : VisualStudioCompletionBroker
    {
        private readonly ICompletionBroker _completionBroker;

        public DefaultVisualStudioCompletionBroker(ICompletionBroker completionBroker!!)
        {
            _completionBroker = completionBroker;
        }

        public override bool IsCompletionActive(ITextView textView!!)
        {
            var completionIsActive = _completionBroker.IsCompletionActive(textView);
            return completionIsActive;
        }
    }
}
