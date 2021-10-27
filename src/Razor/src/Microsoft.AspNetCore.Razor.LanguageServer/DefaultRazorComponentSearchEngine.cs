// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultRazorComponentSearchEngine : RazorComponentSearchEngine
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly ProjectSnapshotManager _projectSnapshotManager;
        private readonly ILogger<DefaultRazorComponentSearchEngine> _logger;

        public DefaultRazorComponentSearchEngine(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor,
            ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher ?? throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            _projectSnapshotManager = projectSnapshotManagerAccessor?.Instance ?? throw new ArgumentNullException(nameof(projectSnapshotManagerAccessor));
            _logger = loggerFactory.CreateLogger<DefaultRazorComponentSearchEngine>();
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
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="tagHelper"/> is null.</exception>
        public override async Task<DocumentSnapshot> TryLocateComponentAsync(TagHelperDescriptor tagHelper)
        {
            if (tagHelper is null)
            {
                throw new ArgumentNullException(nameof(tagHelper));
            }

            if (!DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.TrySplitNamespaceAndType(tagHelper.Name, out var @namespaceName, out var typeName))
            {
                _logger.LogWarning($"Could not split namespace and type for name {tagHelper.Name}.");
                return null;
            }

            var lookupSymbolName = RemoveGenericContent(typeName);

            var projects = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => _projectSnapshotManager.Projects.ToArray(),
                CancellationToken.None).ConfigureAwait(false);

            foreach (var project in projects)
            {
                foreach (var path in project.DocumentFilePaths)
                {
                    // Get document and code document
                    var documentSnapshot = project.GetDocument(path);

                    // Rule out if not Razor component with correct name
                    if (!IsPathCandidateForComponent(documentSnapshot, lookupSymbolName))
                    {
                        continue;
                    }

                    var razorCodeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
                    if (razorCodeDocument is null)
                    {
                        continue;
                    }

                    // Make sure we have the right namespace of the fully qualified name
                    if (!ComponentNamespaceMatchesFullyQualifiedName(razorCodeDocument, namespaceName))
                    {
                        continue;
                    }

                    return documentSnapshot;
                }
            }

            return null;
        }

        private StringSegment RemoveGenericContent(StringSegment typeName)
        {
            var genericSeparatorStart = typeName.IndexOf('<');
            if (genericSeparatorStart > 0)
            {
                var ungenericTypeName = typeName.Subsegment(0, genericSeparatorStart);
                return ungenericTypeName;
            }

            return typeName;
        }

        private static bool IsPathCandidateForComponent(DocumentSnapshot documentSnapshot, StringSegment path)
        {
            if (documentSnapshot.FileKind != FileKinds.Component)
            {
                return false;
            }
            var fileName = Path.GetFileNameWithoutExtension(documentSnapshot.FilePath);
            return new StringSegment(fileName).Equals(path, FilePathComparison.Instance);
        }

        private bool ComponentNamespaceMatchesFullyQualifiedName(RazorCodeDocument razorCodeDocument, StringSegment namespaceName)
        {
            var namespaceNode = (NamespaceDeclarationIntermediateNode)razorCodeDocument
                .GetDocumentIntermediateNode()
                .FindDescendantNodes<IntermediateNode>()
                .First(n => n is NamespaceDeclarationIntermediateNode);

            var namespacesMatch = new StringSegment(namespaceNode.Content).Equals(namespaceName, StringComparison.Ordinal);
            if (!namespacesMatch)
            {
                _logger.LogInformation($"Namespace name {namespaceNode.Content} does not match namespace name {namespaceName}.");
            }

            return namespacesMatch;
        }
    }
}
