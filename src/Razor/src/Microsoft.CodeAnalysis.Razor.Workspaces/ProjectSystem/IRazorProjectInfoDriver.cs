// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

/// <summary>
/// Handles project changes and notifies listeners of project updates and removal.
/// </summary>
internal interface IRazorProjectInfoDriver
{
    ImmutableArray<RazorProjectInfo> GetLatestProjectInfo();

    void AddListener(IRazorProjectInfoListener listener);
}
