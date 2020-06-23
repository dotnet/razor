
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class RefactorComponentCreateCodeActionResolver : RazorCodeActionResolver
    {
        public override string Action => Constants.RefactorComponentCreate;

        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;

        public RefactorComponentCreateCodeActionResolver(
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
            var actionParams = data.ToObject<RefactorComponentCreateParams>();
            var path = actionParams.Uri.GetAbsoluteOrUNCPath();

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

            var newComponentUri = new UriBuilder()
            {
                Scheme = Uri.UriSchemeFile,
                Path = actionParams.Where,
                Host = string.Empty,
            }.Uri;

            var namespaceNode = (NamespaceDeclarationIntermediateNode)codeDocument
               .GetDocumentIntermediateNode()
               .FindDescendantNodes<IntermediateNode>()
               .FirstOrDefault(n => n is NamespaceDeclarationIntermediateNode);

            var changes = new Dictionary<Uri, IEnumerable<TextEdit>>
            {
                [newComponentUri] = new[]
                {
                    new TextEdit()
                    {
                        NewText = $"@namespace {namespaceNode.Content}",
                        Range = new Range(new Position(0, 0), new Position(0, 0)),
                    }
                }
            };
            var documentChanges = new List<WorkspaceEditDocumentChange>
            {
                new WorkspaceEditDocumentChange(new CreateFile() { Uri = newComponentUri.ToString() })
            };
            return new WorkspaceEdit()
            {
                Changes = changes,
                DocumentChanges = documentChanges
            };
        }
    }
}
