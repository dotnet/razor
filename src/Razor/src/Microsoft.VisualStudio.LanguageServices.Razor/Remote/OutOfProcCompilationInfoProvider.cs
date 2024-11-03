// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor.Remote;

[Export(typeof(ICompilationInfoProvider))]
[method: ImportingConstructor]
internal class OutOfProcCompilationInfoProvider(IRemoteServiceInvoker remoteServiceInvoker) : ICompilationInfoProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;

    public async Task<CompilationInfo> GetCompilationInfoAsync(Project project, CancellationToken cancellationToken)
    {
        var result = await _remoteServiceInvoker.TryInvokeAsync<IRemoteCompilationInfoService, CompilationInfo>(
            project.Solution,
            (service, solutionInfo, innerCancellationToken) =>
                service.GetCompilationInfoAsync(solutionInfo, project.Id, innerCancellationToken),
            cancellationToken);

        if (result is { } info)
        {
            return info;
        }

        return new CompilationInfo(HasAddComponentParameter: false);
    }
}
