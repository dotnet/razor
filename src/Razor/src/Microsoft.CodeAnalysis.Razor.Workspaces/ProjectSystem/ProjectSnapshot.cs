﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal abstract class ProjectSnapshot
    {
        public abstract RazorConfiguration Configuration { get; }

        public abstract IEnumerable<string> DocumentFilePaths { get; }

        public abstract string FilePath { get; }

        public virtual string RootNamespace { get; }

        public abstract VersionStamp Version { get; }

        public virtual LanguageVersion CSharpLanguageVersion { get; }

        public virtual IReadOnlyList<TagHelperDescriptor> TagHelpers { get; }

        public virtual ProjectWorkspaceState ProjectWorkspaceState { get; }

        public abstract RazorProjectEngine GetProjectEngine();

        public abstract DocumentSnapshot GetDocument(string filePath);

        public abstract bool IsImportDocument(DocumentSnapshot document);

        /// <summary>
        /// If the provided document is an import document, gets the other documents in the project
        /// that include directives specified by the provided document. Otherwise returns an empty
        /// list.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <returns>A list of related documents.</returns>
        public abstract IEnumerable<DocumentSnapshot> GetRelatedDocuments(DocumentSnapshot document);
    }
}
