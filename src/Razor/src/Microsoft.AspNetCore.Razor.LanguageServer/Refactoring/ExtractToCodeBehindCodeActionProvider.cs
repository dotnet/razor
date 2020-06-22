using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class ExtractToCodeBehindCodeActionProvider : RazorCodeActionProvider
    {
        override public Task<CommandOrCodeActionContainer> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
        {
            if (context.Document.IsUnsupported())
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }

            if (!FileKinds.IsComponent(context.Document.GetFileKind()))
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }

            var change = new SourceChange(context.Location.AbsoluteIndex, length: 0, newText: "");
            var node = context.Document.GetSyntaxTree().Root.LocateOwner(change);
            if (node is null)
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }

            while (!(node is RazorDirectiveSyntax))
            {
                node = node.Parent;
                if (node == null)
                {
                    return Task.FromResult<CommandOrCodeActionContainer>(null);
                }
            }

            if (!(node is RazorDirectiveSyntax))
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }
            var directiveNode = (RazorDirectiveSyntax)node;

            if (directiveNode.DirectiveDescriptor != ComponentCodeDirective.Directive && directiveNode.DirectiveDescriptor != FunctionsDirective.Directive)
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }

            if (node.GetDiagnostics().Any(d => d.Severity == RazorDiagnosticSeverity.Error))
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }

            var cSharpCodeBlockNode = directiveNode.Body.DescendantNodes().FirstOrDefault(n => n is CSharpCodeBlockSyntax);
            if (cSharpCodeBlockNode is null)
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }

            if (cSharpCodeBlockNode.DescendantNodes().Any(n => n is MarkupBlockSyntax || n is CSharpTransitionSyntax || n is RazorCommentBlockSyntax))
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }

            if (context.Location.AbsoluteIndex > cSharpCodeBlockNode.SpanStart)
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }

            var actionParams = new ExtractToCodeBehindParams()
            {
                Uri = context.Request.TextDocument.Uri,
                ExtractStart = cSharpCodeBlockNode.Span.Start,
                ExtractEnd = cSharpCodeBlockNode.Span.End,
                RemoveStart = directiveNode.Span.Start,
                RemoveEnd = directiveNode.Span.End
            };
            var data = JObject.FromObject(actionParams);

            var resolutionParams = new RazorCodeActionResolutionParams()
            {
                Action = Constants.ExtractToCodeBehindAction,  // Extract
                Data = data,
            };
            var serializedParams = JToken.FromObject(resolutionParams);
            var arguments = new JArray(serializedParams);

            var container = new List<CommandOrCodeAction>
            {
                new Command()
                {
                    Title = "Extract code block into backing document",
                    Name = "razor/runCodeAction",
                    Arguments = arguments,
                }
            };

            return Task.FromResult((CommandOrCodeActionContainer)container);
        }
    }
}
