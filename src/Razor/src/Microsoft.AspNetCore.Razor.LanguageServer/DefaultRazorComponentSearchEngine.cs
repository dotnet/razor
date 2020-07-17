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

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultRazorComponentSearchEngine : RazorComponentSearchEngine
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;

        public DefaultRazorComponentSearchEngine(ForegroundDispatcher dispatcher)
        {
            _foregroundDispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>Search for a component in a project based on its tag name and fully qualified name.</summary>
        /// <remarks>
        /// This method makes several assumptions about the nature of components. First, it assumes that a component
        /// a given name `Name` will be located in a file `Name.razor`. Second, it assumes that the namespace the
        /// component is present in has the same name as the assembly its corresponding tag helper is loaded from.
        /// Implicitly, this method inherits any assumptions made by TrySplitNamespaceAndType.
        /// </remarks>
        /// <param name="project">A project to search for the component in.</param>
        /// <param name="componentName">The name of the component.</param>
        /// <param name="fullyQualifiedComponentName">The fully qualified name of the component.</param>
        /// <param name="cancellationToken">A cancellation token, primarily for the document resolver.</param>
        /// <returns>A tuple containing the document URI and related resources loaded during search.</returns>
        public async override Task<Tuple<Uri, DocumentSnapshot, RazorCodeDocument>> TryLocateComponent(ProjectSnapshot project, string componentName, string fullyQualifiedComponentName, CancellationToken cancellationToken)
        {
            foreach (var path in project.DocumentFilePaths)
            {
                // Rule out if not Razor component with correct name
                if (!IsPathCandidateForComponent(path, componentName))
                {
                    continue;
                }

                // Get document and code document
                var documentSnapshot = await Task.Factory.StartNew(() => project.GetDocument(path), cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);
                var razorCodeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);

                // Make sure we have the right namespace of the fully qualified name
                if (!ComponentNamespaceMatchesFullyQualifiedName(razorCodeDocument, fullyQualifiedComponentName))
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

        public static bool IsPathCandidateForComponent(string path, string componentName)
        {
            return Path.GetFileName(path).Equals($"{componentName}.razor", StringComparison.Ordinal);
        }

        public static bool ComponentNamespaceMatchesFullyQualifiedName(RazorCodeDocument razorCodeDocument, string fullyQualifiedComponentName)
        {
            var namespaceNode = (NamespaceDeclarationIntermediateNode)razorCodeDocument
                .GetDocumentIntermediateNode()
                .FindDescendantNodes<IntermediateNode>()
                .FirstOrDefault(n => n is NamespaceDeclarationIntermediateNode);

            DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.TrySplitNamespaceAndType(fullyQualifiedComponentName, out var namespaceNameSpan, out var typeNameSpan);
            var namespaceName = DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.GetTextSpanContent(namespaceNameSpan, fullyQualifiedComponentName);
            return $"{namespaceNode.Content}".Equals(namespaceName, StringComparison.Ordinal);
        }
    }
}
