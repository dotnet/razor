// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.SemanticTokens;
using Microsoft.VisualStudio.Composition;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteClientInitializationServiceFactory : RazorServiceFactoryBase<IRemoteClientInitializationService>
{
    // WARNING: We must always have a parameterless constructor in order to be properly handled by ServiceHub.
    public RemoteClientInitializationServiceFactory()
        : base(RazorServices.Descriptors)
    {
    }

    protected override IRemoteClientInitializationService CreateService(IRazorServiceBroker serviceBroker, ExportProvider exportProvider)
    {
        var languageServerFeatureOptions = exportProvider.GetExportedValue<RemoteLanguageServerFeatureOptions>();
        var semanticTokensLegendService = exportProvider.GetExportedValue<RemoteSemanticTokensLegendService>();
        return new RemoteClientInitializationService(serviceBroker, languageServerFeatureOptions, semanticTokensLegendService);
    }
}
