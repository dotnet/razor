using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    internal class RefactorComponentCodeActionProvider : RazorCodeActionProvider
    {
        private readonly HtmlFactsService _htmlFactsService;
        private readonly ILogger _logger;

        public RefactorComponentCodeActionProvider(
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
            var container = new List<CommandOrCodeAction>();
            var startTagNode = (MarkupStartTagSyntax)context.Document.GetNodeAtLocation(context.Location, n => n.Kind == SyntaxKind.MarkupStartTag);
            if (startTagNode != null)
            {
                var tagName = startTagNode.Name.Content;
                foreach (var diagnostic in context.Document.GetCSharpDocument().Diagnostics)
                {
                    if (diagnostic.Span.AbsoluteIndex <= context.Location.AbsoluteIndex && context.Location.AbsoluteIndex <= diagnostic.Span.AbsoluteIndex + diagnostic.Span.Length)
                    {
                        if (diagnostic.Id == "RZ10012")
                        {
                            var path = context.Request.TextDocument.Uri.GetAbsoluteOrUNCPath();
                            var newComponentPath = Path.Combine(Path.GetDirectoryName(path), $"{tagName}.razor");
                            if (!File.Exists(newComponentPath))
                            {
                                container.Add(CreateComponentFromTag(context, newComponentPath, tagName));
                            }
                        }
                    }
                }
            }

            return Task.FromResult(new CommandOrCodeActionContainer(container));
        }

        private CommandOrCodeAction CreateComponentFromTag(RazorCodeActionContext context, string newComponentPath, string tagName)
        {
            var actionParams = new RefactorComponentCreateParams()
            {
                Uri = context.Request.TextDocument.Uri,
                Name = tagName,
                Where = newComponentPath,
            };
            var data = JObject.FromObject(actionParams);

            var resolutionParams = new RazorCodeActionResolutionParams()
            {
                Action = Constants.RefactorComponentCreate,
                Data = data,
            };
            var serializedParams = JToken.FromObject(resolutionParams);
            var arguments = new JArray(serializedParams);

            return new CommandOrCodeAction(new Command()
            {
                Title = "Create component from tag",
                Name = "razor/runCodeAction",
                Arguments = arguments,
            });
        }
    }
}
