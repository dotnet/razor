﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Resolvers;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal static class RazorServices
{
    // Internal for testing
    internal static readonly IEnumerable<(Type, Type?)> MessagePackServices =
        [
            (typeof(IRemoteLinkedEditingRangeService), null),
            (typeof(IRemoteTagHelperProviderService), null),
            (typeof(IRemoteClientInitializationService), null),
            (typeof(IRemoteSemanticTokensService), null),
            (typeof(IRemoteHtmlDocumentService), null),
            (typeof(IRemoteUriPresentationService), null),
            (typeof(IRemoteFoldingRangeService), null),
            (typeof(IRemoteDocumentHighlightService), null),
            (typeof(IRemoteAutoInsertService), null),
            (typeof(IRemoteFormattingService), null),
        ];

    // Internal for testing
    internal static readonly IEnumerable<(Type, Type?)> JsonServices =
        [
            (typeof(IRemoteGoToDefinitionService), null),
            (typeof(IRemoteSignatureHelpService), null),
            (typeof(IRemoteInlayHintService), null),
            (typeof(IRemoteDocumentSymbolService), null),
            (typeof(IRemoteRenameService), null),
        ];

    private const string ComponentName = "Razor";

    public static readonly RazorServiceDescriptorsWrapper Descriptors = new(
        ComponentName,
        featureDisplayNameProvider: GetFeatureDisplayName,
        additionalFormatters: [],
        additionalResolvers: TopLevelResolvers.All,
        interfaces: MessagePackServices);

    public static readonly RazorServiceDescriptorsWrapper JsonDescriptors = new(
        ComponentName, // Needs to match the above because so much of our ServiceHub infrastructure is convention based
        featureDisplayNameProvider: GetFeatureDisplayName,
        jsonConverters: RazorServiceDescriptorsWrapper.GetLspConverters(),
        interfaces: JsonServices);

    private static string GetFeatureDisplayName(string feature)
    {
        return $"Razor {feature} Feature";
    }
}
