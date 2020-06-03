using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class ExtractToCodeBehindCodeActionProvider : IRazorRefactoringCodeActionProvider
    {
        public Task<CommandOrCodeActionContainer> Provide(RefactoringContext context, CancellationToken cancellationToken)
        {
            var directiveNode = (RazorDirectiveSyntax)context.Document.GetNodeAtLocation(context.Location, n => n.Kind == Language.SyntaxKind.RazorDirective);
            if (directiveNode is null)
            {
                return null;
            }

            var cSharpCodeBlockNode = directiveNode.Body.DescendantNodes().FirstOrDefault(n => n is CSharpCodeBlockSyntax);
            if (cSharpCodeBlockNode is null)
            {
                return null;
            }

            var container = new List<CommandOrCodeAction>();
            container.Add(new Command() {
                Title = "Extract code block into backing document",
                Name = "razor/runCodeAction",
                Arguments = new JArray(
                    "ExtractToCodeBehind",
                    context.Document.Source.FilePath,
                    cSharpCodeBlockNode.Span.Start,
                    cSharpCodeBlockNode.Span.End,
                    directiveNode.Span.Start,
                    directiveNode.Span.End
                ),
            });

            return Task.FromResult((CommandOrCodeActionContainer)container);
        }
    }

    class ExtractToCodeBehindEndpoint : IRazorCodeActionComputationHandler
    {
        private readonly ILogger _logger;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private ExecuteCommandCapability _capability;

        public ExtractToCodeBehindEndpoint(
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            ILoggerFactory loggerFactory)
        {
            if (foregroundDispatcher == null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (documentResolver == null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
            _logger = loggerFactory.CreateLogger<ExtractToCodeBehindEndpoint>();
        }

        public async Task<RazorCodeActionComputationResponse> Handle(RazorCodeActionComputationParams request, CancellationToken cancellationToken)
        {
            _logger.LogDebug(request.ToString());
            _logger.LogDebug("action: " + request.Action);
            if (!string.Equals(request.Action, "ExtractToCodeBehind", StringComparison.Ordinal))
            {
                return null;
            }

            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var filePath = (string)request.Arguments[0];
            var cutStart = Convert.ToInt32(request.Arguments[1]);
            var cutEnd = Convert.ToInt32(request.Arguments[2]);
            var removeStart = Convert.ToInt32(request.Arguments[3]);
            var removeEnd = Convert.ToInt32(request.Arguments[4]);

            var document = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument((string)request.Arguments[0], out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler);

            if (document is null)
            {
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var changes = new Dictionary<Uri, IEnumerable<TextEdit>>
            {
                [new Uri(filePath)] = new[]
                {
                    new TextEdit()
                    {
                        NewText = "",
                        Range = codeDocument.RangeFromIndices(removeStart, removeEnd)
                    }
                }
            };

            return new RazorCodeActionComputationResponse()
            {
                Edit = new WorkspaceEdit() {
                    Changes = changes,
                }
            };
        }
    }
}
