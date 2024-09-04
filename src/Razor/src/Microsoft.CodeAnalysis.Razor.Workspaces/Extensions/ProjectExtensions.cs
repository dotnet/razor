// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.NET.Sdk.Razor.SourceGenerators;

namespace Microsoft.CodeAnalysis;

internal static class ProjectExtensions
{
    public static Document GetRequiredDocument(this Project project, DocumentId documentId)
    {
        return project.GetDocument(documentId)
            ?? ThrowHelper.ThrowInvalidOperationException<Document>($"The document {documentId} did not exist in {project.Name}");
    }

    public static async Task<GeneratorRunResult?> GetRazorGeneratorRunResultAsync(this Project project, CancellationToken cancellationToken)
    {
        var result = await project.GetSourceGeneratorRunResultAsync(cancellationToken).ConfigureAwait(false);
        return result?.Results.SingleOrDefault(r => r.Generator.GetGeneratorType().Name == typeof(RazorSourceGenerator).Name);
    }

#pragma warning disable RSEXPERIMENTAL004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public static async Task<RazorCodeDocument?> TryGetGeneratedRazorCodeDocumentAsync(this Project project, string generatedDocumentHintName, CancellationToken cancellationToken)
    {
        var runResult = await project.GetRazorGeneratorRunResultAsync(cancellationToken).ConfigureAwait(false);
        if (runResult is null)
        {
            // There was no generator, so we couldn't get anything from it
            return null;
        }

        if (!runResult.Value.HostOutputs.TryGetValue(generatedDocumentHintName, out var objectCodeDocument) ||
            objectCodeDocument is not RazorCodeDocument codeDocument)
        {
            return null;
        }

        return codeDocument;
    }
#pragma warning restore RSEXPERIMENTAL004

    public static async Task<RazorCodeDocument?> TryGetGeneratedRazorCodeDocumentAsync(this Project project, Uri generatedDocumentUri, CancellationToken cancellationToken)
    {
        Debug.Assert(generatedDocumentUri.Scheme == "source-generated");

        // TODO: Is it a bad assumption go direct from the Uri to the hint name, by stripping '/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/' etc.
        var sourceGeneratedDocuments = await project.GetSourceGeneratedDocumentsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var document in sourceGeneratedDocuments)
        {
            if (document.CreateUri() == generatedDocumentUri)
            {
                return await project.TryGetGeneratedRazorCodeDocumentAsync(document.HintName, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }
}
