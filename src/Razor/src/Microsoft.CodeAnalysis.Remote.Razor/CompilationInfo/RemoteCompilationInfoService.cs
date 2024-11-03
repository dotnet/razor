// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Compiler.CSharp;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed partial class RemoteCompilationInfoService(in ServiceArgs args) : RazorBrokeredServiceBase(in args), IRemoteCompilationInfoService
{
    internal sealed class Factory : FactoryBase<IRemoteCompilationInfoService>
    {
        protected override IRemoteCompilationInfoService CreateService(in ServiceArgs args)
            => new RemoteCompilationInfoService(in args);
    }

    public ValueTask<CompilationInfo> GetCompilationInfoAsync(RazorPinnedSolutionInfoWrapper solutionInfo, ProjectId projectId, CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            solution => GetCompilationInfoAsync(solution, projectId, cancellationToken),
            cancellationToken);

    private async ValueTask<CompilationInfo> GetCompilationInfoAsync(Solution solution, ProjectId projectId, CancellationToken cancellationToken)
    {
        var project = solution.GetProject(projectId);
        if (project is not null)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is not null)
            {
                return new CompilationInfo(HasAddComponentParameter: compilation.HasAddComponentParameter());
            }
        }

        return new CompilationInfo(HasAddComponentParameter: false);
    }
}
