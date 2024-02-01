// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Editor.Razor;

namespace Microsoft.VisualStudio.LegacyEditor.Razor.Parsing;

internal interface IVisualStudioRazorParserFactory
{
    IVisualStudioRazorParser Create(IVisualStudioDocumentTracker documentTracker);
}
