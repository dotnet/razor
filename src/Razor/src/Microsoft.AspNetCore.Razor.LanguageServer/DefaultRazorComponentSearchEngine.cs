// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
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
        /// <returns>The corresponding DocumentSnapshot if found, null otherwise.</returns>
        public override async Task<DocumentSnapshot> TryLocateComponentAsync(TagHelperDescriptor tagHelper)
        {
            if (tagHelper is null)
            {
                return null;
            }

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
                    var documentSnapshot = project.GetDocument(path);
                    var razorCodeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
                    if (razorCodeDocument is null)
                    {
                        continue;
                    }

                    // Make sure we have the right namespace of the fully qualified name
                    if (!ComponentNamespaceMatchesFullyQualifiedName(razorCodeDocument, tagHelper.Name))
                    {
                        continue;
                    }
                    return documentSnapshot;
                }
            }
            return null;
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
                .First(n => n is NamespaceDeclarationIntermediateNode);

            DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.TrySplitNamespaceAndType(fullyQualifiedComponentName, out var namespaceNameSpan, out var typeNameSpan);
            var namespaceName = DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.GetTextSpanContent(namespaceNameSpan, fullyQualifiedComponentName);
            return $"{namespaceNode.Content}".Equals(namespaceName, StringComparison.Ordinal);
        }
    }
}
