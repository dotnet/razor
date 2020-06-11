using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

using CSharpSyntaxFactory = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class ExtractToCodeBehindCodeActionProvider : RazorCodeActionProvider
    {
        override public Task<CommandOrCodeActionContainer> Provide(RazorCodeActionContext context, CancellationToken cancellationToken)
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

            var extractToCodeBehindParams = new RazorCodeActionResolutionParams()
            {
                Action = "ExtractToCodeBehind",
                Data = new Dictionary<string, object>()
                {
                    { "Uri", new Uri(context.Document.Source.FilePath).ToString() },
                    { "ExtractStart", cSharpCodeBlockNode.Span.Start },
                    { "ExtractEnd", cSharpCodeBlockNode.Span.End },
                    { "RemoveStart", directiveNode.Span.Start },
                    { "RemoveEnd", directiveNode.Span.End }
                },
            };

            var container = new List<CommandOrCodeAction>
            {
                new Command()
                {
                    Title = "Extract code block into backing document",
                    Name = "razor/runCodeAction",
                    Arguments = new JArray(JToken.FromObject(extractToCodeBehindParams))
                }
            };

            return Task.FromResult((CommandOrCodeActionContainer)container);
        }
    }

    class ExtractToCodeBehindCodeActionResolver : RazorCodeActionResolver {
        private readonly ILogger _logger;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;

        public override string Action => "ExtractToCodeBehind";

        public ExtractToCodeBehindCodeActionResolver(
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
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

            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
            _logger = loggerFactory.CreateLogger<ExtractToCodeBehindCodeActionProvider>();
        }

        override public async Task<WorkspaceEdit> Resolve(Dictionary<string, object> data, CancellationToken cancellationToken)
        {
            var uri = new Uri((string)data["Uri"]);
            var cutStart = Convert.ToInt32(data["ExtractStart"]);
            var cutEnd = Convert.ToInt32(data["ExtractEnd"]);
            var removeStart = Convert.ToInt32(data["RemoveStart"]);
            var removeEnd = Convert.ToInt32(data["RemoveEnd"]);

            var path = uri.LocalPath;

            var document = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(path, out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler);

            if (document is null)
            {
                return null;
            }

            var text = await document.GetTextAsync();
            if (text is null)
            {
                return null;
            }

            var contents = text.GetSubTextString(new CodeAnalysis.Text.TextSpan(cutStart, cutEnd - cutStart));

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            _logger.LogInformation("Finishing resolve!");

            var changes = new Dictionary<Uri, IEnumerable<TextEdit>>
            {
                [uri] = new[]
                {
                    new TextEdit()
                    {
                        NewText = "",
                        Range = codeDocument.RangeFromIndices(removeStart, removeEnd)
                    }
                }
            };

            var documentChanges = new List<WorkspaceEditDocumentChange>();

            var codeBehindPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".razor.cs");
            var codeBehindUri = new Uri(codeBehindPath);
            if (!File.Exists(codeBehindPath))
            {
                _logger.LogDebug(codeBehindPath);
                var result = GenerateCodeBehindClass(Path.GetFileNameWithoutExtension(path), contents, codeDocument);
                var unit = result.Item1;
                documentChanges.Add(new WorkspaceEditDocumentChange(new CreateFile() { Uri = codeBehindUri.ToString() }));
                changes[codeBehindUri] = new[]
                {
                    new TextEdit()
                    {
                        NewText = unit.NormalizeWhitespace().ToFullString(),
                        Range = new Range(new Position(0, 0), new Position(0, 0))
                    }
                };
            }

            return new WorkspaceEdit()
            {
                Changes = changes,
                DocumentChanges = documentChanges,
            };
        }

        private IEnumerable<string> FindUsings(RazorCodeDocument razorCodeDocument)
        {
            return razorCodeDocument
                .GetDocumentIntermediateNode()
                .FindDescendantNodes<IntermediateNode>()
                .Where(n => n is UsingDirectiveIntermediateNode)
                .Select(n => ((UsingDirectiveIntermediateNode)n).Content);
        }

        private Tuple<CompilationUnitSyntax, ClassDeclarationSyntax> GenerateCodeBehindClass(string name, string contents, RazorCodeDocument razorCodeDocument)
        {
            var namespaceNode = (NamespaceDeclarationIntermediateNode)razorCodeDocument.GetDocumentIntermediateNode()
                .FindDescendantNodes<IntermediateNode>()
                .FirstOrDefault(n => n is NamespaceDeclarationIntermediateNode);

            if (namespaceNode == null)
            {
                _logger.LogError("Cannot find namespace node!");
            }

            var @class = CSharpSyntaxFactory
                .ClassDeclaration(name)
                .AddModifiers(CSharpSyntaxFactory.ParseToken("public"), CSharpSyntaxFactory.ParseToken("partial"));

            var offset = contents.IndexOf("{") + 1;

            while (true)
            {
                var declaration = CSharpSyntaxFactory.ParseMemberDeclaration(contents, offset, consumeFullText: false);
                if (declaration is null)
                {
                    break;
                }

                offset = declaration.FullSpan.End + 1;
                @class = @class.AddMembers(declaration);
            }

            var @namespace = CSharpSyntaxFactory
                .NamespaceDeclaration(CSharpSyntaxFactory.ParseName(namespaceNode.Content))
                .AddMembers(@class);

            var unit = CSharpSyntaxFactory
                .CompilationUnit()
                .AddUsings(FindUsings(razorCodeDocument).Select(u => CSharpSyntaxFactory.UsingDirective(CSharpSyntaxFactory.ParseName(u))).ToArray())
                .AddMembers(@namespace);

            return Tuple.Create(unit, @class);
        }
    }
}
