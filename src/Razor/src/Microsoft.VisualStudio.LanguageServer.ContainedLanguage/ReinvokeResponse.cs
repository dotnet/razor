// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Client;

#nullable enable

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal class ReinvokeResponse<TOut>
    {
        public ILanguageClient LanguageClient { get; }

        public TOut Result { get; }

        public ReinvokeResponse(
            ILanguageClient languageClient,
            TOut result)
        {
            LanguageClient = languageClient;
            Result = result;
        }
    }
}
