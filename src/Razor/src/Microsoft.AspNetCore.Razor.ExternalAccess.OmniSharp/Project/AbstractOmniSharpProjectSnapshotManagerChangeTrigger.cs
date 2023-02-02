// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;

public abstract class AbstractOmniSharpProjectSnapshotManagerChangeTrigger
{
    public abstract void Initialize(OmniSharpProjectSnapshotManager projectManager);
}
