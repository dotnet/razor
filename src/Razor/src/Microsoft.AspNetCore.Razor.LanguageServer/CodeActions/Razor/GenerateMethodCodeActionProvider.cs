// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis;
using Diagnostic = Microsoft.VisualStudio.LanguageServer.Protocol.Diagnostic;
using SyntaxFacts = Microsoft.CodeAnalysis.CSharp.SyntaxFacts;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;
internal class GenerateMethodCodeActionProvider : IRazorCodeActionProvider
{
    private static readonly Task<IReadOnlyList<RazorVSInternalCodeAction>?> s_emptyResult = Task.FromResult<IReadOnlyList<RazorVSInternalCodeAction>?>(null);

    public Task<IReadOnlyList<RazorVSInternalCodeAction>?> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        var nameNotExistDiagnostics = context.Request.Context.Diagnostics.Where(d => d.Code == "CS0103");
        if (!nameNotExistDiagnostics.Any())
        {
            return s_emptyResult;
        }

        var change = new SourceChange(context.Location.AbsoluteIndex, length: 0, newText: string.Empty);
        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        var owner = syntaxTree.Root.LocateOwner(change);
        if (owner is null)
        {
            return s_emptyResult;
        }

        if (IsGenerateEventHandlerValid(owner, context, nameNotExistDiagnostics, out var @params))
        {
            return Task.FromResult<IReadOnlyList<RazorVSInternalCodeAction>?>(CreateCodeAction(@params));
        }

        return s_emptyResult;
    }

    private static List<RazorVSInternalCodeAction> CreateCodeAction(GenerateMethodCodeActionParams @params)
    {
        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            Action = LanguageServerConstants.CodeActions.GenerateMethod,
            Language = LanguageServerConstants.CodeActions.Languages.Razor,
            Data = @params,
        };

        var codeAction = RazorCodeActionFactory.CreateGenerateMethod(resolutionParams);
        return new List<RazorVSInternalCodeAction> { codeAction };
    }

    private static bool IsGenerateEventHandlerValid(SyntaxNode owner, RazorCodeActionContext context, IEnumerable<Diagnostic> nameNotExistDiagnostics, [NotNullWhen(true)] out GenerateMethodCodeActionParams? @params)
    {
        @params = null;

        // The owner should have a SyntaxKind of CSharpExpressionLiteral or MarkupTextLiteral.
        // MarkupTextLiteral if the cursor is directly before the first letter of the method name.
        // CSharpExpressionalLiteral if cursor is anywhere else in the method name.
        if (owner.Kind != SyntaxKind.CSharpExpressionLiteral && owner.Kind != SyntaxKind.MarkupTextLiteral)
        {
            return false;
        }

        var parent = owner.Kind == SyntaxKind.CSharpExpressionLiteral ? owner.Parent.Parent : owner.Parent;
        if (parent.Kind != SyntaxKind.MarkupTagHelperDirectiveAttribute)
        {
            return false;
        }

        var methodName = string.Empty;
        if (owner.Kind == SyntaxKind.CSharpExpressionLiteral)
        {
            if (!TryParseUndefinedMethodName(owner, nameNotExistDiagnostics, out methodName))
            {
                return false;
            }
        }
        else
        {
            var children = parent.ChildNodes();
            foreach (var child in children)
            {
                if (child.Kind == SyntaxKind.MarkupTagHelperAttributeValue && TryParseUndefinedMethodName(child, nameNotExistDiagnostics, out methodName))
                {
                    break;
                }
            }
        }

        if (methodName.IsNullOrEmpty())
        {
            return false;
        }

        @params = new GenerateMethodCodeActionParams()
        {
            Uri = context.Request.TextDocument.Uri,
            MethodName = methodName,
        };

        return true;
    }

    private static bool TryParseUndefinedMethodName(SyntaxNode node, IEnumerable<Diagnostic> nameNotExistDiagnostics, out string? methodName)
    {
        methodName = null;
        var content = node.GetContent();
        if (!SyntaxFacts.IsValidIdentifier(content))
        {
            return false;
        }

        if (!nameNotExistDiagnostics.Any(d => d.Message == $"The name '{content}' does not exist in the current context"))
        {
            // There is no CS0103 diagnostic dedicated to the method name, meaning that the method is already defined.
            return false;
        }

        methodName = content;
        return true;
    }
}
