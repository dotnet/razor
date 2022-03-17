// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Extensions
{
    internal static class ProjectExtensions
    {
        internal static Document GetRequiredDocument(this Project project!!, DocumentId documentId!!)
        {
            var document = project.GetDocument(documentId);

            if (document is null)
            {
                throw new InvalidOperationException($"The document {documentId} did  not exist in {project.Name}");
            }

            return document;
        }
    }
}
