﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

// The main reason for this service is auto-insert of empty double quotes when a user types
// equals "=" after Blazor component attribute. We think this is Razor (correctly I guess)
// and wouldn't forward auto-insert request to HTML in this case. By essentially overriding
// language info here we allow the request to be sent over to HTML where it will insert empty
// double-quotes as it would for any other attribute value
internal sealed class PreferHtmlInAttributeValuesDocumentPositionInfoStrategy : IDocumentPositionInfoStrategy
{
    public static IDocumentPositionInfoStrategy Instance { get; } = new PreferHtmlInAttributeValuesDocumentPositionInfoStrategy();

    private PreferHtmlInAttributeValuesDocumentPositionInfoStrategy()
    {
    }

    public DocumentPositionInfo GetPositionInfo(IDocumentMappingService mappingService, RazorCodeDocument codeDocument, int hostDocumentIndex)
    {
        var positionInfo = DefaultDocumentPositionInfoStrategy.Instance.GetPositionInfo(mappingService, codeDocument, hostDocumentIndex);

        var absolutePosition = positionInfo.HostDocumentIndex;
        if (positionInfo.LanguageKind != RazorLanguageKind.Razor ||
            absolutePosition < 1)
        {
            return positionInfo;
        }

        // Get the node at previous position to see if we are after markup tag helper attribute,
        // and more specifically after the EqualsToken of it
        var previousPosition = absolutePosition - 1;

        var syntaxTree = codeDocument.GetSyntaxTree().AssumeNotNull();

        var owner = syntaxTree.Root is RazorSyntaxNode root
            ? root.FindInnermostNode(previousPosition)
            : null;

        if (owner is MarkupTagHelperAttributeSyntax { EqualsToken: { IsMissing: false } equalsToken } &&
            equalsToken.EndPosition == positionInfo.HostDocumentIndex)
        {
            return positionInfo with { LanguageKind = RazorLanguageKind.Html };
        }

        return positionInfo;
    }
}
