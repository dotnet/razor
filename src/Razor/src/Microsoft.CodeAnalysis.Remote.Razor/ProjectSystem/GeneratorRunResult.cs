﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.NET.Sdk.Razor.SourceGenerators;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

/// <summary>
/// Wraps the host outputs of the Razor source generator, and the <see cref="CodeAnalysis.Project"/> that produced them.
/// </summary>
internal readonly record struct GeneratorRunResult(RazorGeneratorResult GeneratorResult, Project Project)
{
    public bool IsDefault => GeneratorResult is null && Project is null;

    public IReadOnlyList<TagHelperDescriptor> TagHelpers => GeneratorResult.TagHelpers;

    public RazorCodeDocument? GetCodeDocument(string filePath)
        => GeneratorResult.GetCodeDocument(filePath);

    public RazorCodeDocument GetRequiredCodeDocument(string filePath)
        => GeneratorResult.GetCodeDocument(filePath)
           ?? throw new InvalidOperationException(SR.FormatGenerator_run_result_did_not_contain_a_code_document(filePath));

    public string? GetRazorFilePathFromHintName(string generatedDocumentHintName)
        => GeneratorResult.GetFilePath(generatedDocumentHintName);

    public bool TryGetRazorDocument(string razorFilePath, out TextDocument? document)
        => Project.Solution.TryGetRazorDocument(razorFilePath, out document);

    public async Task<SourceGeneratedDocument> GetRequiredSourceGeneratedDocumentForRazorFilePathAsync(string filePath, CancellationToken cancellationToken)
    {
        var hintName = GeneratorResult.GetHintName(filePath);

        var generatedDocument = await Project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName, cancellationToken).ConfigureAwait(false);

        return generatedDocument
            ?? throw new InvalidOperationException(SR.FormatCouldnt_get_the_source_generated_document_for_hint_name(hintName));
    }

    public static async Task<GeneratorRunResult> CreateAsync(bool throwIfNotFound, Project project, RemoteSnapshotManager snapshotManager, CancellationToken cancellationToken)
    {
        var result = await project.GetSourceGeneratorRunResultAsync(cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            if (throwIfNotFound)
            {
                throw new InvalidOperationException(SR.FormatCouldnt_get_a_source_generator_run_result(project.Name));
            }

            return default;
        }

        var runResult = result.Results.SingleOrDefault(r => r.Generator.GetGeneratorType().Assembly.Location == typeof(RazorSourceGenerator).Assembly.Location);
        if (runResult.Generator is null)
        {
            if (throwIfNotFound)
            {
                if (result.Results.SingleOrDefault(r => r.Generator.GetGeneratorType().Name == "Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator").Generator is { } wrongGenerator)
                {
                    // Wrong ALC?
                    throw new InvalidOperationException(SR.FormatRazor_source_generator_reference_incorrect(wrongGenerator.GetGeneratorType().Assembly.Location, typeof(RazorSourceGenerator).Assembly.Location, project.Name));
                }
                else
                {
                    throw new InvalidOperationException(SR.FormatRazor_source_generator_is_not_referenced(project.Name));
                }
            }

            return default;
        }

#pragma warning disable RSEXPERIMENTAL004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        if (!runResult.HostOutputs.TryGetValue(nameof(RazorGeneratorResult), out var objectResult))
#pragma warning restore RSEXPERIMENTAL004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        {
            // We know the generator is referenced, or we wouldn't have gotten past the above checks. We also know cohosting is turned on, since we got here.
            // There is a race condition that can happen where if Roslyn runs the generator before Razor has a chance to initialize, then Roslyn will have
            // cached the fact that cohosting was off, and any subsequent runs of the generator will not produce a host output. We can work around this by
            // making an innocuous change to the project, and trying again. The snapshot manager makes this change inside a lock, and if we're not the first
            // ones to try, we'll get the updated project which may have already had generators run on it. We recurse back into ourselves here with the updated
            // project to try again. TryGetRetryProject also protects against infinite recursion.
            if (snapshotManager.TryGetRetryProject(project) is { } retryProject)
            {
                return await CreateAsync(throwIfNotFound, retryProject, snapshotManager, cancellationToken).ConfigureAwait(false);
            }

            if (throwIfNotFound)
            {
                throw new InvalidOperationException(SR.FormatRazor_source_generator_did_not_produce_a_host_output(project.Name, string.Join(Environment.NewLine, runResult.Diagnostics)));
            }

            return default;
        }

        if (objectResult is not RazorGeneratorResult generatorResult)
        {
            if (throwIfNotFound)
            {
                // Wrong ALC?
                throw new InvalidOperationException(SR.FormatRazor_source_generator_host_output_is_not_RazorGeneratorResult(project.Name, string.Join(Environment.NewLine, runResult.Diagnostics)));
            }

            return default;
        }

        return new(generatorResult, project);
    }
}
