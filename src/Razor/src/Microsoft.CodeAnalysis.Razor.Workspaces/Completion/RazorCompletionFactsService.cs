// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Completion
{
    internal abstract class RazorCompletionFactsService
    {
        public abstract IReadOnlyList<RazorCompletionItem> GetCompletionItems(RazorCompletionContext razorCompletionContext);
    }
}
