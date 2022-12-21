// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;

internal static class IRazorGeneratedDocumentExtensions
{
    public static SourceText GetSourceText(this IRazorGeneratedDocument generatedDocument)
        => generatedDocument.CodeDocument.GetGeneratedSourceText(generatedDocument);
}
