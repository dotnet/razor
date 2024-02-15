// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.LanguageServer.Client;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage;

internal struct ReinvokeResponse<TOut>
{
    [Obsolete("Will be removed in a future version.")]
    public ILanguageClient LanguageClient { get; } = null!;

    public TOut Result { get; }

    [Obsolete("Will be removed in a future version.")]
    public bool IsSuccess => LanguageClient != default;

    [Obsolete("Will be removed in a future version.")]
    public ReinvokeResponse(
        ILanguageClient languageClient,
        TOut result)
    {
        LanguageClient = languageClient;
        Result = result;
    }

    public ReinvokeResponse(TOut result)
    {
        Result = result;
    }
}
