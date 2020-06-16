using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class CreateComponentFromTagCodeActionProvider : RazorCodeActionProvider
    {
        private readonly HtmlFactsService _htmlFactsService;
        private readonly ILogger _logger;

        public CreateComponentFromTagCodeActionProvider(
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

        override public Task<CommandOrCodeActionContainer> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
        {
            var startTagNode = (MarkupStartTagSyntax)context.Document.GetNodeAtLocation(context.Location, n => n.Kind == SyntaxKind.MarkupStartTag);
            foreach (var diagnostic in context.Document.GetCSharpDocument().Diagnostics)
            {
                if (diagnostic.Span.AbsoluteIndex <= context.Location.AbsoluteIndex && context.Location.AbsoluteIndex <= diagnostic.Span.AbsoluteIndex + diagnostic.Span.Length)
                {
                    if (diagnostic.Id == "RZ10012")
                    {
                        // startTagNode.Name.Content

                    }
                }
            }

            return null;
        }
    }
}
