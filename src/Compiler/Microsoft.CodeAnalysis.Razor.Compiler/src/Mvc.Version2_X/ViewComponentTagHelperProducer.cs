// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.TagHelpers.Producers;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;

internal sealed partial class ViewComponentTagHelperProducer : TagHelperProducer
{
    private readonly ViewComponentTagHelperDescriptorFactory _factory;
    private readonly INamedTypeSymbol _viewComponentAttributeType;
    private readonly INamedTypeSymbol? _nonViewComponentAttributeType;

    private ViewComponentTagHelperProducer(
        ViewComponentTagHelperDescriptorFactory factory,
        INamedTypeSymbol viewComponentAttributeType,
        INamedTypeSymbol? nonViewComponentAttributeType)
    {
        _factory = factory;
        _viewComponentAttributeType = viewComponentAttributeType;
        _nonViewComponentAttributeType = nonViewComponentAttributeType;
    }

    public override bool HandlesAssembly(IAssemblySymbol assembly) => true;

    public override bool SupportsTypeProcessing => true;

    public override bool IsCandidateType(INamedTypeSymbol type)
        => type.IsViewComponent(_viewComponentAttributeType, _nonViewComponentAttributeType);

    public override void AddTagHelpersForType(
        INamedTypeSymbol type,
        ICollection<TagHelperDescriptor> results,
        CancellationToken cancellationToken)
    {
        if (_factory.CreateDescriptor(type) is { } descriptor)
        {
            results.Add(descriptor);
        }
    }
}
