using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultRazorComponentSearchEngine : RazorComponentSearchEngine
    {
        private readonly ProjectSnapshotManager _projectSnapshotManager;

        public DefaultRazorComponentSearchEngine(ProjectSnapshotManager projectSnapshotManager)
        {
            _projectSnapshotManager = projectSnapshotManager ?? throw new ArgumentNullException(nameof(projectSnapshotManager));
        }

        /// <summary>Search for a component in a project based on its tag name and fully qualified name.</summary>
        /// <remarks>
        /// This method makes several assumptions about the nature of components. First, it assumes that a component
        /// a given name `Name` will be located in a file `Name.razor`. Second, it assumes that the namespace the
        /// component is present in has the same name as the assembly its corresponding tag helper is loaded from.
        /// Implicitly, this method inherits any assumptions made by TrySplitNamespaceAndType.
        /// </remarks>
        /// <param name="tagHelper">A TagHelperDescriptor to find the corresponding Razor component for.</param>
        /// <param name="documentSnapshot">A document snapshot if found, null otherwise.</param>
        /// <returns>A tuple containing the document URI and related resources loaded during search.</returns>
        public override bool TryLocateComponent(TagHelperDescriptor tagHelper, out DocumentSnapshot documentSnapshot)
        {
            DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.TrySplitNamespaceAndType(tagHelper.Name, out _, out var typeSpan);
            var typeName = tagHelper.Name.Substring(typeSpan.Start, typeSpan.Length);

            foreach (var project in _projectSnapshotManager.Projects)
            {
                if (!project.FilePath.EndsWith($"{tagHelper.AssemblyName}.csproj", FilePathComparison.Instance))
                {
                    continue;
                }

                foreach (var path in project.DocumentFilePaths)
                {
                    // Rule out if not Razor component with correct name
                    if (!IsPathCandidateForComponent(path, typeName))
                    {
                        continue;
                    }

                    // Get document and code document
                    documentSnapshot = project.GetDocument(path);
                    if (!documentSnapshot.TryGetGeneratedOutput(out var razorCodeDocument))
                    {
                        continue;
                    }

                    // Make sure we have the right namespace of the fully qualified name
                    if (!ComponentNamespaceMatchesFullyQualifiedName(razorCodeDocument, tagHelper.Name))
                    {
                        continue;
                    }
                    return true;
                }
            }
            documentSnapshot = null;
            return false;
        }

        public static bool IsPathCandidateForComponent(string path, string componentName)
        {
            return path.EndsWith($"{componentName}.razor", FilePathComparison.Instance);
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
