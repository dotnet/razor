// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage;

internal class ReinvocationResponse<TResponseType>
{
    public ReinvocationResponse(TResponseType? response)
    {
        Response = response;
    }

    [Obsolete("Will be removed in a future version.")]
    public ReinvocationResponse(string languageClientName, TResponseType? response)
    {
        LanguageClientName = languageClientName;
        Response = response;
    }

    [Obsolete("Will be removed in a future version.")]
    public string LanguageClientName { get; } = null!;

    public TResponseType? Response { get; }
}
