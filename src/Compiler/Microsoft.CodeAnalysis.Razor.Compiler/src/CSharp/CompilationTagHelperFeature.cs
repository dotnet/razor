﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.CodeAnalysis.Razor;

public sealed class CompilationTagHelperFeature : RazorEngineFeatureBase, ITagHelperFeature
{
    private ITagHelperDescriptorProvider[] _providers;
    private IMetadataReferenceFeature _referenceFeature;

    public IReadOnlyList<TagHelperDescriptor> GetDescriptors()
    {
        var results = new List<TagHelperDescriptor>();

        var context = TagHelperDescriptorProviderContext.Create(results);
        var compilation = CSharpCompilation.Create("__TagHelpers", references: _referenceFeature.References);
        if (IsValidCompilation(compilation))
        {
            context.SetCompilation(compilation);
        }

        for (var i = 0; i < _providers.Length; i++)
        {
            _providers[i].Execute(context);
        }

        return results;
    }

    protected override void OnInitialized()
    {
        _referenceFeature = Engine.Features.OfType<IMetadataReferenceFeature>().FirstOrDefault();
        _providers = Engine.Features.OfType<ITagHelperDescriptorProvider>().OrderBy(f => f.Order).ToArray();
    }

    internal static bool IsValidCompilation(Compilation compilation)
    {
        var @string = compilation.GetSpecialType(SpecialType.System_String);

        // Do some minimal tests to verify the compilation is valid. If symbols for System.String
        // is missing or errored, the compilation may be missing references.
        return @string != null && @string.TypeKind != TypeKind.Error;
    }
}
