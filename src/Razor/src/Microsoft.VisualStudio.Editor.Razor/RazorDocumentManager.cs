// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.Editor.Razor
{
    internal abstract class RazorDocumentManager : ILanguageService
    {
        public abstract Task OnTextViewOpenedAsync(ITextView textView, IEnumerable<ITextBuffer> subjectBuffers);

        public abstract Task OnTextViewClosedAsync(ITextView textView, IEnumerable<ITextBuffer> subjectBuffers);
    }
}
