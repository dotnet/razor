// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;

public abstract class OmniSharpProjectSnapshotManagerAccessor
{
    internal abstract OmniSharpProjectSnapshotManager Instance { get; }
}
