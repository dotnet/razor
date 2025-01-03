﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using SyntaxFacts = Microsoft.CodeAnalysis.CSharp.SyntaxFacts;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

internal class GenerateMethodCodeActionProvider : IRazorCodeActionProvider
{
    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        var nameNotExistDiagnostics = context.Request.Context.Diagnostics.Any(d => d.Code == "CS0103");
        if (!nameNotExistDiagnostics)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        var owner = syntaxTree.Root.FindToken(context.StartAbsoluteIndex).Parent.AssumeNotNull();

        if (IsGenerateEventHandlerValid(owner, out var methodName, out var eventName, out var eventParameterType))
        {
            var textDocument = context.Request.TextDocument;
            return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>(
                [
                    RazorCodeActionFactory.CreateGenerateMethod(textDocument, context.DelegatedDocumentUri, methodName, eventName, eventParameterType),
                    RazorCodeActionFactory.CreateAsyncGenerateMethod(textDocument, context.DelegatedDocumentUri, methodName, eventName, eventParameterType)
                ]);
        }

        return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
    }

    private static bool IsGenerateEventHandlerValid(
        SyntaxNode owner,
        [NotNullWhen(true)] out string? methodName,
        [NotNullWhen(true)] out string? eventName,
        out string? eventParameterType)
    {
        methodName = null;
        eventName = null;
        eventParameterType = null;

        // The owner should have a SyntaxKind of CSharpExpressionLiteral or MarkupTextLiteral.
        // MarkupTextLiteral if the cursor is directly before the first letter of the method name.
        // CSharpExpressionalLiteral if cursor is anywhere else in the method name.
        if (owner.Kind != SyntaxKind.CSharpExpressionLiteral && owner.Kind != SyntaxKind.MarkupTextLiteral)
        {
            return false;
        }

        // We want to get MarkupTagHelperDirectiveAttribute since this has information about the event name.
        // Hierarchy:
        // MarkupTagHelper[Directive]Attribute > MarkupTextLiteral
        // or
        // MarkupTagHelper[Directive]Attribute > MarkupTagHelperAttributeValue > CSharpExpressionLiteral
        var commonParent = owner.Kind == SyntaxKind.CSharpExpressionLiteral ? owner.Parent.Parent : owner.Parent;

        // MarkupTagHelperElement > MarkupTagHelperStartTag > MarkupTagHelperDirectiveAttribute
        if (commonParent.Parent.Parent is not MarkupTagHelperElementSyntax { TagHelperInfo.BindingResult: var binding })
        {
            return false;
        }

        return commonParent switch
        {
            MarkupTagHelperDirectiveAttributeSyntax markupTagHelperDirectiveAttribute => TryGetEventNameAndMethodName(markupTagHelperDirectiveAttribute, binding, out methodName, out eventName, out eventParameterType),
            MarkupTagHelperAttributeSyntax markupTagHelperAttribute => TryGetEventNameAndMethodName(markupTagHelperAttribute, binding, out methodName, out eventName, out eventParameterType),
            _ => false
        };
    }

    private static bool TryGetEventNameAndMethodName(
        MarkupTagHelperDirectiveAttributeSyntax markupTagHelperDirectiveAttribute,
        TagHelperBinding binding,
        [NotNullWhen(true)] out string? methodName,
        [NotNullWhen(true)] out string? eventName,
        out string? eventParameterType)
    {
        methodName = null;
        eventName = null;
        eventParameterType = null;

        foreach (var tagHelperDescriptor in binding.Descriptors)
        {
            foreach (var attribute in tagHelperDescriptor.BoundAttributes)
            {
                if (attribute.Name == markupTagHelperDirectiveAttribute.TagHelperAttributeInfo.Name)
                {
                    // We found the attribute that matches the directive attribute, now we need to check if the
                    // tag helper it's bound to is an event handler. This filters out things like @ref and @rendermode
                    if (!tagHelperDescriptor.IsEventHandlerTagHelper())
                    {
                        return false;
                    }

                    eventParameterType = tagHelperDescriptor.GetEventArgsType() ?? "";

                    break;
                }
            }
        }

        if (markupTagHelperDirectiveAttribute.TagHelperAttributeInfo.ParameterName is not null)
        {
            // An event parameter is being set instead of the event handler e.g.
            // <button @onclick:preventDefault=SomeValue/>, this is not a generate event handler scenario.
            return false;
        }

        // The TagHelperAttributeInfo Name property includes the '@' in the beginning so exclude it.
        eventName = markupTagHelperDirectiveAttribute.TagHelperAttributeInfo.Name[1..];

        var content = markupTagHelperDirectiveAttribute.Value.GetContent();
        if (!SyntaxFacts.IsValidIdentifier(content))
        {
            return false;
        }

        methodName = content;
        return true;
    }

    private static bool TryGetEventNameAndMethodName(
        MarkupTagHelperAttributeSyntax markupTagHelperDirectiveAttribute,
        TagHelperBinding binding,
        [NotNullWhen(true)] out string? methodName,
        [NotNullWhen(true)] out string? eventName,
        out string? eventParameterType)
    {
        methodName = null;
        eventName = null;
        eventParameterType = null;

        foreach (var tagHelperDescriptor in binding.Descriptors)
        {
            foreach (var attribute in tagHelperDescriptor.BoundAttributes)
            {
                if (attribute.Name == markupTagHelperDirectiveAttribute.TagHelperAttributeInfo.Name)
                {
                    if (!attribute.IsEventCallbackProperty())
                    {
                        return false;
                    }

                    // TypeName is something like "EventCallback<System.String>", so we need to parse out the parameter type.
                    if (ComponentAttributeIntermediateNode.TryGetEventCallbackArgument(attribute.TypeName.AsMemory(), out var argument))
                    {
                        eventParameterType = argument.ToString();
                    }

                    break;
                }
            }
        }

        eventName = markupTagHelperDirectiveAttribute.TagHelperAttributeInfo.Name;

        var content = markupTagHelperDirectiveAttribute.Value.GetContent();
        if (!SyntaxFacts.IsValidIdentifier(content))
        {
            return false;
        }

        methodName = content;
        return true;
    }
}
