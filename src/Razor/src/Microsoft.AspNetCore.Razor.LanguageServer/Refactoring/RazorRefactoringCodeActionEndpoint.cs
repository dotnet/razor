
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class RazorRefactoringCodeActionEndpoint : ICodeActionHandler
    {
        private readonly ILogger _logger;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorSyntaxService _syntaxService;
        private CodeActionCapability _capability;

        public RazorRefactoringCodeActionEndpoint(
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            RazorSyntaxService syntaxService,
            ILoggerFactory loggerFactory)
        {
            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (syntaxService is null)
            {
                throw new ArgumentNullException(nameof(syntaxService));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
            _syntaxService = syntaxService;
            _logger = loggerFactory.CreateLogger<RazorRefactoringEndpoint>();
            _logger.LogDebug("Instantiated RazorRefactoringEndpoint");
        }

        public CodeActionRegistrationOptions GetRegistrationOptions()
        {
            return new CodeActionRegistrationOptions()
            {
                DocumentSelector = RazorDefaults.Selector
            };
        }

        public async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var document = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

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

            var sourceText = await document.GetTextAsync();
            var linePosition = new LinePosition((int)request.Range.Start.Line, (int)request.Range.Start.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var location = new SourceLocation(hostDocumentIndex, (int)request.Range.Start.Line, (int)request.Range.Start.Character);
            var node = _syntaxService.GetNode(codeDocument, location);

            if (node.Kind == SyntaxKind.RazorMetaCode)
            {
                return HandleExtractMetaCode(request, codeDocument, node);
            }

            return null;
        }

        private CommandOrCodeActionContainer HandleExtractMetaCode(CodeActionParams request, RazorCodeDocument codeDocument, SyntaxNode node)
        {
            if (node.Parent.Kind != SyntaxKind.RazorDirectiveBody)
            {
                return null;
            }

            var directiveBodyNode = (RazorDirectiveBodySyntax)node.Parent;

            // Not sure why this needs to occur twice, it's just how the syntax tree is built
            var cSharpCodeBlockNode = GetChildCSharpCodeBlock(GetChildCSharpCodeBlock(directiveBodyNode));

            if (cSharpCodeBlockNode is null)
            {
                _logger.LogError($"Could not find expected code block!");
                return null;
            }

            var directiveBody = codeDocument.GetSourceText().GetSubTextString(new CodeAnalysis.Text.TextSpan(
                cSharpCodeBlockNode.Span.Start,
                cSharpCodeBlockNode.Span.Length));
            var directiveBodyNodeLocation = node.GetSourceLocation(codeDocument.Source);

            var changes = new Dictionary<Uri, IEnumerable<TextEdit>>();
            changes[request.TextDocument.Uri] = new[]
            {
                new TextEdit()
                {
                    NewText = "",
                    Range = new Range()
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

        private CSharpCodeBlockSyntax GetChildCSharpCodeBlock(SyntaxNode node)
        {
            if (node is null)
            {
                return null;
            }

            foreach (var childNode in node.ChildNodes())
            {
                if (childNode.Kind == SyntaxKind.CSharpCodeBlock)
                {
                    return (CSharpCodeBlockSyntax)childNode;
                }
            }
            return null;
        }

        public void SetCapability(CodeActionCapability capability)
        {
            _capability = capability;
        }
    }
}
