﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal class MarkupTransitionCompletionDescription : CompletionDescription
{
    public override string Description { get; }

    public MarkupTransitionCompletionDescription(string description)
    {
        if (description is null)
        {
            throw new ArgumentNullException(nameof(description));
        }

        Description = description;
    }
}
