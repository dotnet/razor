// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Resolvers;

internal static class RazorResolvers
{
    public static readonly ImmutableArray<IMessagePackFormatter> TagHelperFormatters = ImmutableArray.Create<IMessagePackFormatter>(
        AllowedChildTagFormatter.Instance,
        BoundAttributeFormatter.Instance,
        BoundAttributeParameterFormatter.Instance,
        DocumentationObjectFormatter.Instance,
        MetadataCollectionFormatter.Instance,
        RequiredAttributeFormatter.Instance,
        TagHelperFormatter.Instance,
        TagMatchingRuleFormatter.Instance,
        RazorDiagnosticFormatter.Instance);

    public static readonly ImmutableArray<IMessagePackFormatter> RazorProjectInfoFormatters = ImmutableArray.Create<IMessagePackFormatter>(
        DocumentSnapshotHandleFormatter.Instance,
        ProjectWorkspaceStateFormatter.Instance,
        RazorConfigurationFormatter.Instance,
        RazorProjectInfoFormatter.Instance)
        .AddRange(TagHelperFormatters);

    public static readonly IFormatterResolver TopLevel = CompositeResolver.Create(
        ChecksumResolver.Instance,
        RazorProjectInfoResolver.Instance,
        ProjectSnapshotHandleResolver.Instance,
        TagHelperDeltaResultResolver.Instance);
}
