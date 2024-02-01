// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Utilities;
using Moq;

namespace Microsoft.VisualStudio.LegacyEditor.Razor.Test;

internal static class ContentTypes
{
    public static readonly IContentType RazorCore =
        StrictMock.Of<IContentType>(c =>
            c.IsOfType(RazorLanguage.CoreContentType) == true);

    public static readonly IContentType NonRazorCore =
        StrictMock.Of<IContentType>(c =>
            c.IsOfType(It.IsAny<string>()) == false);
}
