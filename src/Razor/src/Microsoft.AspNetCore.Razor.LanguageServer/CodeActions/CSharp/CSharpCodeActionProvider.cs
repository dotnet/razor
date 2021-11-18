// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal abstract class CSharpCodeActionProvider
    {
        protected static readonly Task<IReadOnlyList<RazorCodeAction>> EmptyResult =
            Task.FromResult(Array.Empty<RazorCodeAction>() as IReadOnlyList<RazorCodeAction>);

        public abstract Task<IReadOnlyList<RazorCodeAction>> ProvideAsync(
            RazorCodeActionContext context,
            IEnumerable<RazorCodeAction> codeActions,
            CancellationToken cancellationToken);

        protected static bool InFunctionsBlockThatCantHaveCodeActions(RazorCodeActionContext context)
        {
            var change = new SourceChange(context.Location.AbsoluteIndex, length: 0, newText: string.Empty);
            var syntaxTree = context.CodeDocument.GetSyntaxTree();
            if (syntaxTree?.Root is null)
            {
                return false;
            }

            var owner = syntaxTree.Root.LocateOwner(change);
            if (owner is null)
            {
                Debug.Fail("Owner should never be null.");
                return false;
            }

            var node = owner.Ancestors().FirstOrDefault(n => n.Kind == SyntaxKind.RazorDirective);
            if (node is not RazorDirectiveSyntax directiveNode)
            {
                return false;
            }

            if (directiveNode.DirectiveDescriptor != FunctionsDirective.Directive)
            {
                return false;
            }

            // At this point we know its a functions block, but because of how the source mappings work,
            // if the opening brace for the functions block is not on the same line as the functions node itself
            // then we can offer code actions.
            //
            // This is because when we have a block like this:
            //
            // @functions {
            //    class Goo { }
            // }
            //
            // The source mapping starts at char 13 on the "@functions" line (after the open brace). Unfortunately
            // and code that is needed on that line, say an attribute that the code action wants to insert, will
            // start at char 8 because of the indentation of the generated code. This means it starts outside of the
            // mapping, so is thrown away, which results in data loss.
            //
            // When the open brace is on the next line, the source mapping starts at char 2, so the insertion at char 8
            // is fine.
            if (directiveNode.Body is RazorDirectiveBodySyntax directiveBody &&
                directiveBody.CSharpCode.Children.TryGetOpenBraceNode(out var openBrace))
            {
                context.SourceText.GetLineAndOffset(directiveNode.SpanStart, out var directiveLine, out _);
                context.SourceText.GetLineAndOffset(openBrace.SpanStart, out var braceLine, out _);
                if (braceLine > directiveLine)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
