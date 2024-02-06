// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Moq;

namespace Microsoft.VisualStudio.LegacyEditor.Razor;

internal static class VsMocks
{
    public static ITextBuffer CreateTextBuffer(bool core)
        => CreateTextBuffer(core ? ContentTypes.RazorCore : ContentTypes.NonRazor);

    public static ITextBuffer CreateTextBuffer(IContentType contentType, PropertyCollection? propertyCollection = null)
    {
        propertyCollection ??= new PropertyCollection();

        return StrictMock.Of<ITextBuffer>(b =>
            b.ContentType == contentType &&
            b.Properties == propertyCollection);
    }

    internal static class ContentTypes
    {
        public static readonly IContentType LegacyRazorCore = Create(RazorConstants.LegacyCoreContentType);
        public static readonly IContentType RazorCore = Create(RazorLanguage.CoreContentType);
        public static readonly IContentType NonRazor = StrictMock.Of<IContentType>(c => c.IsOfType(It.IsAny<string>()) == false);

        public static IContentType Create(params string[] types)
        {
            var mock = new StrictMock<IContentType>();
            mock.Setup(x => x.IsOfType(It.IsAny<string>()))
                .Returns((string type) => Array.IndexOf(types, type) >= 0);

            return mock.Object;
        }
    }
}
