// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Composition;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteSignatureHelpServiceFactory : RazorServiceFactoryBase<IRemoteSignatureHelpService>
{
    // WARNING: We must always have a parameterless constructor in order to be properly handled by ServiceHub.
    public RemoteSignatureHelpServiceFactory()
        : base(RazorServices.JsonDescriptors)
    {
    }

    protected override IRemoteSignatureHelpService CreateService(IServiceBroker serviceBroker, ExportProvider exportProvider)
    {
        var documentSnapshotFactory = exportProvider.GetExportedValue<DocumentSnapshotFactory>();
        var filePathService = exportProvider.GetExportedValue<IFilePathService>();
        var documentMappingService = exportProvider.GetExportedValue<IRazorDocumentMappingService>();
        return new RemoteSignatureHelpService(serviceBroker, documentSnapshotFactory, filePathService, documentMappingService);
    }
}
