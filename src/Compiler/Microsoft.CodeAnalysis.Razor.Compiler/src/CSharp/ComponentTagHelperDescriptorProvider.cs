// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.TagHelpers.Producers;

namespace Microsoft.CodeAnalysis.Razor;

internal sealed class ComponentTagHelperDescriptorProvider : TagHelperDescriptorProviderBase
{
    public override void Execute(TagHelperDescriptorProviderContext context, CancellationToken cancellationToken = default)
    {
        ArgHelper.ThrowIfNull(context);

        var compilation = context.Compilation;
        var factory = GetRequiredFeature<ComponentTagHelperProducer.Factory>();

        if (!factory.TryCreate(compilation, context.IncludeDocumentation, context.ExcludeHidden, out var producer))
        {
            return;
        }

        var targetAssembly = context.TargetAssembly;

        var collector = new Collector(compilation, targetAssembly, producer);
        collector.Collect(context, cancellationToken);
    }

    private sealed class Collector(
        Compilation compilation,
        IAssemblySymbol? targetAssembly,
        TagHelperProducer producer)
        : TagHelperCollector<Collector>(compilation, targetAssembly)
    {
        protected override bool IsCandidateType(INamedTypeSymbol type)
            => producer.IsCandidateType(type);

        protected override void Collect(
            INamedTypeSymbol type,
            ICollection<TagHelperDescriptor> results,
            CancellationToken cancellationToken)
            => producer.AddTagHelpersForType(type, results, cancellationToken);
    }
}
