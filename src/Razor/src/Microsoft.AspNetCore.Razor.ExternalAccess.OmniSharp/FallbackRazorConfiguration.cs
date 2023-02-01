// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp;

internal class FallbackRazorConfiguration
{
    internal static RazorConfiguration SelectConfiguration(Version version) => CodeAnalysis.Razor.ProjectSystem.FallbackRazorConfiguration.SelectConfiguration(version);
}
