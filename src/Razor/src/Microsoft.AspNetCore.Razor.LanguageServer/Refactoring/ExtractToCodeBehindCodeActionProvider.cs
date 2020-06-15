using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class ExtractToCodeBehindCodeActionProvider : RazorCodeActionProvider
    {
        override public Task<CommandOrCodeActionContainer> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
        {
            var directiveNode = (RazorDirectiveSyntax)context.Document.GetNodeAtLocation(context.Location, n => n.Kind == SyntaxKind.RazorDirective);
            if (directiveNode is null)
            {
                return null;
            }

            if (directiveNode.DirectiveDescriptor.Directive != "code" && directiveNode.DirectiveDescriptor.Directive != "function")
            {
                return null;
            }

            var cSharpCodeBlockNode = directiveNode.Body.DescendantNodes().FirstOrDefault(n => n is CSharpCodeBlockSyntax);
            if (cSharpCodeBlockNode is null)
            {
                return null;
            }

            if (cSharpCodeBlockNode.DescendantNodes().Any(n => n is MarkupBlockSyntax || n is CSharpTransitionSyntax || n is RazorCommentBlockSyntax))
            {
                return null;
            }

            // Only if hovering over @code
            if (context.Location.AbsoluteIndex > cSharpCodeBlockNode.SpanStart)
            {
                return null;
            }

            var actionParams = new ExtractToCodeBehindParams()
            {
                Uri = new Uri(context.Document.Source.FilePath),
                ExtractStart = cSharpCodeBlockNode.Span.Start,
                ExtractEnd = cSharpCodeBlockNode.Span.End,
                RemoveStart = directiveNode.Span.Start,
                RemoveEnd = directiveNode.Span.End
            };
            var data = JObject.FromObject(actionParams);

            var resolutionParams = new RazorCodeActionResolutionParams()
            {
                Action = "ExtractToCodeBehind",  // Extract
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
