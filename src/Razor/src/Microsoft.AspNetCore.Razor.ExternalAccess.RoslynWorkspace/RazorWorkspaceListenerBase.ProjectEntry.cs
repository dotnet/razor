// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Utilities;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.RoslynWorkspace;

public abstract partial class RazorWorkspaceListenerBase
{
    internal class ProjectEntry
    {
        public int? TagHelpersResultId { get; set; }
        public Checksum? ProjectChecksum { get; set; }
    }
}
