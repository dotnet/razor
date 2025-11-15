// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.TagHelpers.Producers;

namespace Microsoft.CodeAnalysis.Razor;

internal sealed class SplatTagHelperDescriptorProvider : TagHelperDescriptorProviderBase
{
    public override void Execute(TagHelperDescriptorProviderContext context, CancellationToken cancellationToken = default)
    {
        ArgHelper.ThrowIfNull(context);

        var factory = GetRequiredFeature<SplatTagHelperProducer.Factory>();

        if (!factory.TryCreate(context.Compilation, context.IncludeDocumentation, context.ExcludeHidden, out var producer))
        {
            return;
        }

        var targetAssembly = context.TargetAssembly;

        if (targetAssembly is not null && !producer.HandlesAssembly(targetAssembly))
        {
            return;
        }

        producer.AddStaticTagHelpers(context.Results);
    }
}
