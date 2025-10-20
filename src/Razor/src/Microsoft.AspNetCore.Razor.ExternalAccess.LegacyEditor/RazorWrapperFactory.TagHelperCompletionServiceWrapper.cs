// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Completion;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.LegacyEditor;

internal static partial class RazorWrapperFactory
{
    private class TagHelperCompletionServiceWrapper(ITagHelperCompletionService obj) : Wrapper<ITagHelperCompletionService>(obj), IRazorTagHelperCompletionService
    {
        public IRazorElementCompletionContext CreateContext(IRazorTagHelperDocumentContext tagHelperContext, IEnumerable<string>? existingCompletions, string? containingTagName, IEnumerable<KeyValuePair<string, string>> attributes, string? containingParentTagName, bool containingParentIsTagHelper, Func<string, bool> inHTMLSchema)
            => Wrap(
                new ElementCompletionContext(
                    Unwrap(tagHelperContext),
                    existingCompletions,
                    containingTagName,
                    attributes.ToImmutableArray(),
                    containingParentTagName,
                    containingParentIsTagHelper,
                    inHTMLSchema));

        public ImmutableDictionary<string, ImmutableArray<IRazorTagHelperDescriptor>> GetElementCompletions(IRazorElementCompletionContext completionContext)
        {
            var result = Object.GetElementCompletions(Unwrap(completionContext));

            // Use StringComparer.Ordinal for the output dictionary, which implements both IComparer and IEqualityComparer
            var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<IRazorTagHelperDescriptor>>(StringComparer.Ordinal);
            foreach (var (key, value) in result.Completions)
            {
                builder.Add(key, WrapAll(value, Wrap));
            }

            return builder.ToImmutable();
        }
    }
}
