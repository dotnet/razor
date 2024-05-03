﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;

namespace Microsoft.CodeAnalysis.Razor.DocumentPresentation;

internal static class UriPresentationHelper
{
    public static Uri? GetComponentFileNameFromUriPresentationRequest(RazorLanguageKind languageKind, Uri[]? uris, ILogger logger)
    {
        if (languageKind is not RazorLanguageKind.Html)
        {
            // Component tags can only be inserted into Html contexts, so if this isn't Html there is nothing we can do.
            return null;
        }

        if (uris is null || uris.Length == 0)
        {
            logger.LogDebug($"No URIs were included in the request?");
            return null;
        }

        var razorFileUri = uris.Where(
            x => Path.GetFileName(x.GetAbsoluteOrUNCPath()).EndsWith(".razor", FilePathComparison.Instance)).FirstOrDefault();

        // We only want to handle requests for a single .razor file, but when there are files nested under a .razor
        // file (for example, Goo.razor.css, Goo.razor.cs etc.) then we'll get all of those files as well, when the user
        // thinks they're just dragging the parent one, so we have to be a little bit clever with the filter here
        if (razorFileUri == null)
        {
            logger.LogDebug($"No file in the drop was a razor file URI.");
            return null;
        }

        var fileName = Path.GetFileName(razorFileUri.GetAbsoluteOrUNCPath());
        if (uris.Any(uri => !Path.GetFileName(uri.GetAbsoluteOrUNCPath()).StartsWith(fileName, FilePathComparison.Instance)))
        {
            logger.LogDebug($"One or more URIs were not a child file of the main .razor file.");
            return null;
        }

        return razorFileUri;
    }
}
