// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CommonLanguageServerProtocol.Framework;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Cohost;

internal class CohostEndpointAttribute : LanguageServerEndpointAttribute
{
    public CohostEndpointAttribute(string method)
        : base(method, Constants.RazorLanguageName)
    {
    }
}
