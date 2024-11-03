// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal interface ICompilationInfoProvider
{
    Task<CompilationInfo> GetCompilationInfoAsync(Project workspaceProject, CancellationToken cancellationToken);
}
