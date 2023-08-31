// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Resolvers;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal static class RazorServiceDescriptors
{
    private const string ComponentName = "Razor";

    public static readonly RazorServiceDescriptorsWrapper TagHelperProviderServiceDescriptors = new(
        ComponentName,
        featureDisplayNameProvider: _ => "Razor TagHelper Provider",
        additionalFormatters: ImmutableArray.Create<IMessagePackFormatter>(
            ChecksumFormatter.Instance,
            DocumentSnapshotHandleFormatter.Instance,
            ProjectSnapshotHandleFormatter.Instance,
            ProjectWorkspaceStateFormatter.Instance,
            RazorConfigurationFormatter.Instance,
            RazorDiagnosticFormatter.Instance,
            RazorProjectInfoFormatter.Instance,
            TagHelperDeltaResultFormatter.Instance,
            AllowedChildTagFormatter.Instance,
            BoundAttributeFormatter.Instance,
            BoundAttributeParameterFormatter.Instance,
            DocumentationObjectFormatter.Instance,
            MetadataCollectionFormatter.Instance,
            RequiredAttributeFormatter.Instance,
            TagHelperFormatter.Instance,
            TagMatchingRuleFormatter.Instance),
        additionalResolvers: ImmutableArray.Create<IFormatterResolver>(
            ChecksumResolver.Instance,
            ProjectSnapshotHandleResolver.Instance,
            RazorProjectInfoResolver.Instance,
            TagHelperDeltaResultResolver.Instance),
        interfaces: new (Type, Type?)[] { (typeof(IRemoteTagHelperProviderService), null) });
}
