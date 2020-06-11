using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class RazorRefactoringCodeActionEndpoint : ICodeActionHandler, IRazorCodeActionResolutionHandler
    {
        private readonly IEnumerable<RazorCodeActionProvider> _providers;
        private readonly Dictionary<string, RazorCodeActionResolver> _resolvers;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly ILogger _logger;

        private CodeActionCapability _capability;

        public RazorRefactoringCodeActionEndpoint(
            IEnumerable<RazorCodeActionProvider> providers,
            IEnumerable<RazorCodeActionResolver> resolvers,
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            ILoggerFactory loggerFactory)
        {
            if (providers is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (resolvers is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

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

            _providers = providers;
            _resolvers = new Dictionary<string, RazorCodeActionResolver>();
            foreach (var resolver in resolvers)
            {
                if (_resolvers.ContainsKey(resolver.Action))
                {
                    _logger.LogError($"duplicate resolver action for {resolver.Action}");
                }
                _resolvers[resolver.Action] = resolver;
            }

            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
            _logger = loggerFactory.CreateLogger<RazorRefactoringCodeActionEndpoint>();
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

            var context = new RazorCodeActionContext(request, codeDocument, location);
            var tasks = new List<Task<CommandOrCodeActionContainer>>();
                
            foreach (var provider in _providers)
            {
                var result = provider.Provide(context, cancellationToken);
                if (result != null)
                {
                    tasks.Add(result);
                }
            }

            var results = await Task.WhenAll(tasks);
            var container = new List<CommandOrCodeAction>();
            foreach (var result in results)
            {
                if (result != null)
                {
                    foreach (var commandOrCodeAction in result)
                    {
                        container.Add(commandOrCodeAction);
                    }
                }
            }

            return container;
        }

        public async Task<RazorCodeActionResolutionResponse> Handle(RazorCodeActionResolutionParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _logger.LogDebug($"Resolving action {request.Action} with data {request.Data}");
            if (!_resolvers.ContainsKey(request.Action))
            {
                _logger.LogError($"No resolver registered for {request.Action}");
                return new RazorCodeActionResolutionResponse() { Edit = null };
            }

            var edit = await _resolvers[request.Action].Resolve(request.Data, cancellationToken);
            return new RazorCodeActionResolutionResponse() { Edit = edit };
        }

        public void SetCapability(CodeActionCapability capability)
        {
            _capability = capability;
        }
    }
}
