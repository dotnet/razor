// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.Remote;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed partial class RemoteLinkedEditingRangeService
{
    internal sealed class Factory : FactoryBase<IRemoteLinkedEditingRangeService>
    {
        protected override IRemoteLinkedEditingRangeService CreateService(in ServiceArgs args)
            => new RemoteLinkedEditingRangeService(in args);
    }
}
