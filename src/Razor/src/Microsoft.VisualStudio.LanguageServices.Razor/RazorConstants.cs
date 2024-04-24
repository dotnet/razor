﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.VisualStudio.Razor;

internal static class RazorConstants
{
    public const string LegacyContentType = "LegacyRazorCSharp";

    public const string LegacyCoreContentType = "LegacyRazorCoreCSharp";

    public const string RazorLSPContentTypeName = "Razor";

    public const string RazorLanguageServiceString = "4513FA64-5B72-4B58-9D4C-1D3C81996C2C";

    public const string RazorCohostingUIContext = "6d5b86dc-6b8a-483b-ae30-098a3c7d6774";

    public static readonly Guid RazorLanguageServiceGuid = new(RazorLanguageServiceString);

    public const string VSProjectItemsIdentifier = "CF_VSSTGPROJECTITEMS";
}
