using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class CreateComponentFromTag : RazorCodeActionProvider
    {
        private readonly HtmlFactsService _htmlFactsService;
        private readonly ILogger _logger;

        public CreateComponentFromTag(
            HtmlFactsService htmlFactsService,
            ILoggerFactory loggerFactory)
        {
            if (htmlFactsService is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _htmlFactsService = htmlFactsService;
            _logger = loggerFactory.CreateLogger<ExtractToCodeBehindCodeActionProvider>();
        }

        override public Task<CommandOrCodeActionContainer> Provide(RazorCodeActionContext context, CancellationToken cancellationToken)
        {
            var startTagNode = (MarkupStartTagSyntax)context.Document.GetNodeAtLocation(context.Location, n => n.Kind == Language.SyntaxKind.MarkupStartTag);

            if (_htmlFactsService.TryGetElementInfo(startTagNode, out var containingTagNameToken, out var attributes) &&
                containingTagNameToken.Span.IntersectsWith(context.Location.AbsoluteIndex))
            {
                
            }

            return null;
        }
    }
}
