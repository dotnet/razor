using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class ExtractToCodeBehindCodeActionProvider : IRazorRefactoringCodeActionProvider
    {
        public async Task<CommandOrCodeActionContainer> Provide(RefactoringContext context, CancellationToken cancellationToken)
        {
            var directiveNode = (RazorDirectiveSyntax)context.Document.GetNodeAtLocation(context.Location, n => n.Kind == SyntaxKind.RazorDirective);
            if (directiveNode is null)
            {
                return null;
            }

            var cSharpCodeBlockNode = directiveNode.Body.DescendantNodes().FirstOrDefault(n => n is CSharpCodeBlockSyntax);
            if (cSharpCodeBlockNode is null)
            {
                return null;
            }

            var directiveContents = context.Document.GetSourceText().GetSubTextString(cSharpCodeBlockNode.Span.AsCodeAnalysisTextSpan());

            var changes = new Dictionary<Uri, IEnumerable<TextEdit>>
            {
                [context.Request.TextDocument.Uri] = new[]
                {
                    new TextEdit()
                    {
                        NewText = "",
                        Range = directiveNode.AsRange(context.Document)
                    }
                }
            };

            return new CommandOrCodeActionContainer(new CodeAction()
            {
                Title = "Extract code block into backing document",
                Edit = new WorkspaceEdit()
                {
                    Changes = changes,
                }
            });
        }
    }

    class ExtractToCodeBehindEndpoint : IExecuteCommandHandler
    {
        public ExecuteCommandRegistrationOptions GetRegistrationOptions()
        {
            throw new NotImplementedException();
        }

        public Task<Unit> Handle(ExecuteCommandParams request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void SetCapability(ExecuteCommandCapability capability)
        {
            throw new NotImplementedException();
        }
    }
}
