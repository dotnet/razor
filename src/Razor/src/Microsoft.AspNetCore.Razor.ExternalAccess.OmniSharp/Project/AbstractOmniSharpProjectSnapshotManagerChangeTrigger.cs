// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;

internal abstract class AbstractOmniSharpProjectSnapshotManagerChangeTrigger
{
    internal abstract void Initialize(OmniSharpProjectSnapshotManagerBase projectManager);
}
