// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.VisualStudio.Composition;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteAutoInsertServiceFactory : RazorServiceFactoryBase<IRemoteAutoInsertService>
{
    // WARNING: We must always have a parameterless constructor in order to be properly handled by ServiceHub.
    public RemoteAutoInsertServiceFactory()
        : base(RazorServices.Descriptors)
    {
    }

    protected override IRemoteAutoInsertService CreateService(IRazorServiceBroker serviceBroker, ExportProvider exportProvider)
    {
        var infoService = exportProvider.GetExportedValue<IAutoInsertService>();
        var documentSnapshotFactory = exportProvider.GetExportedValue<DocumentSnapshotFactory>();
        return new RemoteAutoInsertService(serviceBroker, documentSnapshotFactory, infoService);
    }
}
