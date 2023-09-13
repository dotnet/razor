// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal sealed class StaticCompilationTagHelperFeature(Compilation compilation)
        : RazorEngineFeatureBase, ITagHelperFeature
    {
        private ImmutableArray<ITagHelperDescriptorProvider> _providers;

        public void GetDescriptors(ISymbol? targetSymbol, List<TagHelperDescriptor> results)
        {
            var context = TagHelperDescriptorProviderContext.Create(results);
            context.SetCompilation(compilation);

            if (targetSymbol is not null)
            {
                context.Items.SetTargetSymbol(targetSymbol);
            }

            foreach (var provider in _providers)
            {
                provider.Execute(context);
            }
        }

        IReadOnlyList<TagHelperDescriptor> ITagHelperFeature.GetDescriptors()
        {
            var results = new List<TagHelperDescriptor>();
            GetDescriptors(targetSymbol: null, results);

            return results;
        }

        protected override void OnInitialized()
        {
            _providers = Engine.Features
                .OfType<ITagHelperDescriptorProvider>()
                .OrderBy(f => f.Order)
                .ToImmutableArray();
        }
    }
}
