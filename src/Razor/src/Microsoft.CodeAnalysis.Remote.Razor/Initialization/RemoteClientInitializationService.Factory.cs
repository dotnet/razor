// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.Remote;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed partial class RemoteClientInitializationService
{
    internal sealed class Factory : FactoryBase<IRemoteClientInitializationService>
    {
        protected override IRemoteClientInitializationService CreateService(in ServiceArgs args)
            => new RemoteClientInitializationService(in args);
    }
}
