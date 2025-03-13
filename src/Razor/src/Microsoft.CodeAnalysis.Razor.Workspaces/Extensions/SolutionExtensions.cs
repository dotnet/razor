// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.CodeAnalysis;

internal static class SolutionExtensions
{
    public static ImmutableArray<DocumentId> GetDocumentIdsWithUri(this Solution solution, Uri uri)
    {
        Debug.Assert(RazorUri.IsGeneratedDocumentUri(uri) == false, "This won't work with source generated Uris");
        return solution.GetDocumentIdsWithFilePath(uri.GetDocumentFilePath());
    }

    public static bool TryGetRazorDocument(this Solution solution, Uri razorDocumentUri, [NotNullWhen(true)] out TextDocument? razorDocument)
    {
        var razorDocumentId = solution.GetDocumentIdsWithUri(razorDocumentUri).FirstOrDefault();

        // If we couldn't locate the .razor file, just return the generated file.
        if (razorDocumentId is null ||
            solution.GetAdditionalDocument(razorDocumentId) is not TextDocument document)
        {
            razorDocument = null;
            return false;
        }

        razorDocument = document;
        return true;
    }
}
