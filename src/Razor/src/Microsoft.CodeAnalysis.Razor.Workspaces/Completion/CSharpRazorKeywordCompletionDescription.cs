// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal class CSharpRazorKeywordCompletionDescription : CompletionDescription
{
    public override string Description { get; }

    public CSharpRazorKeywordCompletionDescription(string description)
    {
        ArgHelper.ThrowIfNull(description);

        Description = string.Format(SR.KeywordDescription, description);
    }
}
