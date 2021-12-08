// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LiveShare;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    // This type must be a public interface in order to properly advertise itself as part of the LiveShare ICollaborationService infrastructure.
    public interface IRemoteHierarchyService : ICollaborationService
    {
        public Task<bool> HasCapabilityAsync(Uri pathOfFileInProject, string capability, CancellationToken cancellationToken);
    }
}
