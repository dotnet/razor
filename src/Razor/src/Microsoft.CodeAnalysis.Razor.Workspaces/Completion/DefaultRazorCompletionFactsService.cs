// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Completion
{
    [Shared]
    [Export(typeof(RazorCompletionFactsService))]
    internal class DefaultRazorCompletionFactsService : RazorCompletionFactsService
    {
        private readonly IReadOnlyList<RazorCompletionItemProvider> _completionItemProviders;

        [ImportingConstructor]
        public DefaultRazorCompletionFactsService([ImportMany] IEnumerable<RazorCompletionItemProvider> completionItemProviders)
        {
            if (completionItemProviders is null)
            {
                throw new ArgumentNullException(nameof(completionItemProviders));
            }

            _completionItemProviders = completionItemProviders.ToArray();
        }

        public override IReadOnlyList<RazorCompletionItem> GetCompletionItems(RazorCompletionContext context, SourceSpan location)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.TagHelperDocumentContext is null)
            {
                throw new ArgumentNullException(nameof(context.TagHelperDocumentContext));
            }

            var completions = new List<RazorCompletionItem>();
            for (var i = 0; i < _completionItemProviders.Count; i++)
            {
                var completionItemProvider = _completionItemProviders[i];
                var items = completionItemProvider.GetCompletionItems(context, location);
                completions.AddRange(items);
            }

            return completions;
        }
    }
}
