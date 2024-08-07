﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.SemanticTokens;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

internal class RazorSemanticTokensLegendService(IClientCapabilitiesService clientCapabilitiesService)
    : AbstractRazorSemanticTokensLegendService(clientCapabilitiesService)
{
}
