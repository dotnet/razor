using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    internal class DefaultRazorComponentSearchEngine : RazorComponentSearchEngine
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;

        public DefaultRazorComponentSearchEngine(ForegroundDispatcher dispatcher)
        {
            _foregroundDispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public async override Task<Tuple<Uri, DocumentSnapshot, RazorCodeDocument>> TryLocateComponent(ProjectSnapshot project, string name, string fullyQualifiedName, CancellationToken cancellationToken)
        {
            foreach (var path in project.DocumentFilePaths)
            {
                // Rule out if not Razor component with correct name
                if (!Path.GetFileName(path).Equals($"{name}.razor", StringComparison.Ordinal))
                {
                    continue;
                }

                // Get document and code document
                var documentSnapshot = await Task.Factory.StartNew(() => project.GetDocument(path), cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);
                var razorCodeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);

                var namespaceNode = (NamespaceDeclarationIntermediateNode)razorCodeDocument
                    .GetDocumentIntermediateNode()
                    .FindDescendantNodes<IntermediateNode>()
                    .FirstOrDefault(n => n is NamespaceDeclarationIntermediateNode);

                // Make sure we have the right namespace of the fully qualified name
                DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.TrySplitNamespaceAndType(fullyQualifiedName, out var namespaceNameSpan, out var typeNameSpan);
                var namespaceName = DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.GetTextSpanContent(namespaceNameSpan, fullyQualifiedName);
                var typeName = DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.GetTextSpanContent(typeNameSpan, fullyQualifiedName);
                if (!$"{namespaceNode.Content}".Equals(namespaceName, StringComparison.Ordinal))
                {
                    continue;
                }

                var uri = new UriBuilder
                {
                    Scheme = "file",
                    Path = path,
                    Host = string.Empty,
                }.Uri;
                return Tuple.Create(uri, documentSnapshot, razorCodeDocument);
            }
            return null;
        }
    }
}
