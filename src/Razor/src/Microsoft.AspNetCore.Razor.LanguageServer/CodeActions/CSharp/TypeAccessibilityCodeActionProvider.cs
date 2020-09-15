// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class TypeAccessibilityCodeActionProvider : CSharpCodeActionProvider
    {
        private static readonly Task<RazorCodeAction[]> EmptyResult = Task.FromResult<RazorCodeAction[]>(null);

        public override Task<RazorCodeAction[]> ProvideAsync(RazorCodeActionContext context, IEnumerable<RazorCodeAction> codeActions, CancellationToken cancellationToken)
        {
            var diagnostic = context.Request.Context.Diagnostics.FirstOrDefault(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error &&
                (diagnostic.Code?.IsString ?? false) &&

                // `The type or namespace name 'type/namespace' could not be found
                //  (are you missing a using directive or an assembly reference?)`
                // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs0246
                (diagnostic.Code.Value.String.Equals("CS0246", StringComparison.OrdinalIgnoreCase) ||

                // `The name 'identifier' does not exist in the current context`
                // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs0103
                diagnostic.Code.Value.String.Equals("CS0103", StringComparison.OrdinalIgnoreCase)));

            if (diagnostic is null)
            {
                return default;
            }

            var diagnosticSpan = diagnostic.Range.AsTextSpan(context.SourceText);
            var associatedValue = context.SourceText.GetSubTextString(diagnosticSpan);

            var results = new HashSet<RazorCodeAction>();

            foreach (var codeAction in codeActions)
            {
                if (!codeAction.Title.Any(c => char.IsWhiteSpace(c)) &&
                    codeAction.Title.EndsWith(associatedValue, StringComparison.OrdinalIgnoreCase))
                {
                    CreateFQNCodeAction(context, diagnostic, codeAction, results);
                    CreateAddUsingCodeAction(context, codeAction, results);
                }
            }

            return Task.FromResult(results.ToArray());
        }

        private static void CreateFQNCodeAction(
            RazorCodeActionContext context,
            Diagnostic fqnDiagnostic,
            RazorCodeAction codeAction,
            ICollection<RazorCodeAction> results)
        {
            var fqnWorkspaceEdit = new WorkspaceEdit()
            {
                Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>()
                {
                    {
                        context.Request.TextDocument.Uri,
                        new List<TextEdit>()
                        {
                            new TextEdit()
                            {
                                NewText = codeAction.Title,
                                Range = fqnDiagnostic.Range
                            }
                        }
                    }
                }
            };

            var fqnCodeAction = new RazorCodeAction()
            {
                Title = codeAction.Title,
                Edit = fqnWorkspaceEdit
            };
            results.Add(fqnCodeAction);
        }

        private static void CreateAddUsingCodeAction(
            RazorCodeActionContext context,
            RazorCodeAction codeAction,
            ICollection<RazorCodeAction> results)
        {
            if (!DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.TrySplitNamespaceAndType(
                    codeAction.Title,
                    out var @namespaceSpan,
                    out _))
            {
                return;
            }

            var @namespace = codeAction.Title.Substring(@namespaceSpan.Start, @namespaceSpan.Length);
            var addUsingStatement = $"@using {@namespace}";

            var codeDocumentIdentifier = new VersionedTextDocumentIdentifier() { Uri = context.Request.TextDocument.Uri };
            var addUsingWorkspaceEdit = AddUsingsCodeActionHelper.CreateAddUsingWorkspaceEdit(@namespace, context.CodeDocument, codeDocumentIdentifier);

            var addUsingCodeAction = new RazorCodeAction()
            {
                Title = addUsingStatement,
                Edit = addUsingWorkspaceEdit
            };
            results.Add(addUsingCodeAction);
        }

        private bool IsTagUnknown(MarkupStartTagSyntax startTag, RazorCodeActionContext context)
        {
            foreach (var diagnostic in context.CodeDocument.GetCSharpDocument().Diagnostics)
            {
                // Check that the diagnostic is to do with our start tag
                if (!(diagnostic.Span.AbsoluteIndex > startTag.Span.End
                    || startTag.Span.Start > diagnostic.Span.AbsoluteIndex + diagnostic.Span.Length))
                {
                    // Component is not recognized in environment
                    if (diagnostic.Id == ComponentDiagnosticFactory.UnexpectedMarkupElement.Id)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
