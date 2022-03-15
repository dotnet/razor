// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;

namespace Microsoft.CodeAnalysis.Razor.Completion
{
    internal class DirectiveCompletionDescription : CompletionDescription
    {
        public override string Description { get; }

        public DirectiveCompletionDescription(string description!!)
        {
            Description = description;
        }
    }
}
