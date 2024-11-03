// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteCompilationInfoService
{
    ValueTask<CompilationInfo> GetCompilationInfoAsync(RazorPinnedSolutionInfoWrapper solutionInfo, ProjectId projectId, CancellationToken cancellationToken);
}
