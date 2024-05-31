// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal interface IRazorProjectInfoListener
{
    ValueTask RemovedAsync(ProjectKey projectKey, CancellationToken cancellationToken);
    ValueTask UpdatedAsync(RazorProjectInfo projectInfo, CancellationToken cancellationToken);
}
