// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal interface IRazorProjectInfoPublisher
{
    ImmutableArray<RazorProjectInfo> GetLatestProjects();

    void AddListener(IRazorProjectInfoListener listener);
}
