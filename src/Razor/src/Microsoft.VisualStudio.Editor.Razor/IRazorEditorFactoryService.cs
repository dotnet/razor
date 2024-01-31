// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Editor.Razor;

internal interface IRazorEditorFactoryService
{
    bool TryGetDocumentTracker(ITextBuffer textBuffer, [NotNullWhen(true)] out IVisualStudioDocumentTracker? documentTracker);
    bool TryGetParser(ITextBuffer textBuffer, [NotNullWhen(true)] out VisualStudioRazorParser? parser);
    bool TryGetSmartIndenter(ITextBuffer textBuffer, [NotNullWhen(true)] out BraceSmartIndenter? braceSmartIndenter);
}
