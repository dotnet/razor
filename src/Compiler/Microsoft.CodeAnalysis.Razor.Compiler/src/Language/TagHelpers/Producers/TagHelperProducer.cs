// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language.TagHelpers.Producers;

internal abstract class TagHelperProducer
{
    public abstract class FactoryBase : RazorEngineFeatureBase, IRazorEngineFeature, ITagHelperProducerFactory
    {
        public bool TryCreate(Compilation compilation, [NotNullWhen(true)] out TagHelperProducer? result)
            => TryCreate(compilation, includeDocumentation: false, excludeHidden: false, out result);

        public abstract bool TryCreate(
            Compilation compilation,
            bool includeDocumentation,
            bool excludeHidden,
            [NotNullWhen(true)] out TagHelperProducer? result);
    }

    public abstract bool HandlesAssembly(IAssemblySymbol assembly);

    public virtual bool SupportsStaticTagHelpers => false;

    public virtual void AddStaticTagHelpers(ICollection<TagHelperDescriptor> results)
    {
    }

    public virtual bool SupportsTypeProcessing => false;

    public virtual bool IsCandidateType(INamedTypeSymbol type) => false;

    public virtual void AddTagHelpersForType(
        INamedTypeSymbol type,
        ICollection<TagHelperDescriptor> results,
        CancellationToken cancellationToken)
    {
    }
}
