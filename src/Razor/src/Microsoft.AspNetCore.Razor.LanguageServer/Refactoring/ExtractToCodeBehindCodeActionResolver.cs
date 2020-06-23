using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Razor;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

using CSharpSyntaxFactory = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    internal class ExtractToCodeBehindCodeActionResolver : RazorCodeActionResolver
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;

        public override string Action => Constants.ExtractToCodeBehindAction;

        public ExtractToCodeBehindCodeActionResolver(
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver)
        {
            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
        }

        public override async Task<WorkspaceEdit> ResolveAsync(JObject data, CancellationToken cancellationToken)
        {
            var actionParams = data.ToObject<ExtractToCodeBehindParams>();
            var path = Path.GetFullPath(actionParams.Uri.LocalPath);

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

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            if (!FileKinds.IsComponent(codeDocument.GetFileKind()))
            {
                return null;
            }

            var codeBehindPath = "";
            var n = 0;
            do
            {
                var identifier = n > 0 ? n.ToString() : "";  // Make it look nice
                codeBehindPath = Path.Combine(
                    Path.GetDirectoryName(path),
                    $"{Path.GetFileNameWithoutExtension(path)}{identifier}{Path.GetExtension(path)}.cs");
                n++;
            } while (File.Exists(codeBehindPath));

            var codeBehindUri = new UriBuilder()
            {
                Scheme = Uri.UriSchemeFile,
                Path = codeBehindPath,
                Host = string.Empty,
            }.Uri;

            var className = Path.GetFileNameWithoutExtension(path);
            var contents = text.GetSubTextString(new CodeAnalysis.Text.TextSpan(actionParams.ExtractStart, actionParams.ExtractEnd - actionParams.ExtractStart));
            var compilationUnit = GenerateCodeBehindClass(className, contents, codeDocument);

            var changes = new Dictionary<Uri, IEnumerable<TextEdit>>
            {
                [actionParams.Uri] = new[]
                {
                    new TextEdit()
                    {
                        NewText = "",
                        Range = codeDocument.RangeFromIndices(actionParams.RemoveStart, actionParams.RemoveEnd)
                    }
                },
                [codeBehindUri] = new[]
                {
                    new TextEdit()
                    {
                        NewText = compilationUnit.NormalizeWhitespace().ToFullString(),
                        Range = new Range(new Position(0, 0), new Position(0, 0))
                    }
                }
            };

            var documentChanges = new List<WorkspaceEditDocumentChange>
            {
                new WorkspaceEditDocumentChange(new CreateFile() { Uri = codeBehindUri.ToString() })
            };
            
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

        private CompilationUnitSyntax GenerateCodeBehindClass(string className, string contents, RazorCodeDocument razorCodeDocument)
        {
            var namespaceNode = (NamespaceDeclarationIntermediateNode)razorCodeDocument
                .GetDocumentIntermediateNode()
                .FindDescendantNodes<IntermediateNode>()
                .FirstOrDefault(n => n is NamespaceDeclarationIntermediateNode);

            var @class = CSharpSyntaxFactory
                .ClassDeclaration(className)
                .AddModifiers(CSharpSyntaxFactory.ParseToken("public"), CSharpSyntaxFactory.ParseToken("partial"));

            var mock = (ClassDeclarationSyntax)CSharpSyntaxFactory.ParseMemberDeclaration($"class Class {contents}");
            @class = @class.AddMembers(mock.Members.ToArray());

            var @namespace = CSharpSyntaxFactory
                .NamespaceDeclaration(CSharpSyntaxFactory.ParseName(namespaceNode.Content))
                .AddMembers(@class);

            var compilationUnit = CSharpSyntaxFactory
                .CompilationUnit()
                .AddUsings(FindUsings(razorCodeDocument).Select(u => CSharpSyntaxFactory.UsingDirective(CSharpSyntaxFactory.ParseName(u))).ToArray())
                .AddMembers(@namespace);

            return compilationUnit;
        }
    }
}
