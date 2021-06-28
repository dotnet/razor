// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal enum OmniSharpVSCompletionInvokeKind
    {
        Explicit = 0,
        Typing = 1,
        Deletion = 2
    }
}
