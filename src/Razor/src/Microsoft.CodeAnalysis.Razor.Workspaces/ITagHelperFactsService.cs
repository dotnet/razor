// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.VisualStudio.Editor.Razor;

internal interface ITagHelperFactsService
{
    TagHelperBinding? GetTagHelperBinding(TagHelperDocumentContext documentContext, string? tagName, IEnumerable<KeyValuePair<string, string>> attributes, string? parentTag, bool parentIsTagHelper);

    IEnumerable<BoundAttributeDescriptor> GetBoundTagHelperAttributes(TagHelperDocumentContext documentContext, string attributeName, TagHelperBinding binding);

    IReadOnlyList<TagHelperDescriptor> GetTagHelpersGivenTag(TagHelperDocumentContext documentContext, string tagName, string? parentTag);

    IReadOnlyList<TagHelperDescriptor> GetTagHelpersGivenParent(TagHelperDocumentContext documentContext, string? parentTag);

    List<KeyValuePair<string, string>> StringifyAttributes(SyntaxList<RazorSyntaxNode> attributes);

    (string? ancestorTagName, bool ancestorIsTagHelper) GetNearestAncestorTagInfo(IEnumerable<SyntaxNode> ancestors);
}
