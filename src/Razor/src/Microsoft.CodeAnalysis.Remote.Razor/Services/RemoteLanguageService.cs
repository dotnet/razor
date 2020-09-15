// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.ServiceHub.Framework;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    internal sealed class RemoteLanguageService : RazorServiceBase, IRemoteLanguageService
    {
        internal sealed class Factory : FactoryBase<IRemoteLanguageService>
        {
            protected override IRemoteLanguageService CreateService(IServiceBroker serviceBroker)
                => new RemoteLanguageService(serviceBroker);
        }

        private RemoteLanguageService(IServiceBroker serviceBroker)
            : base(serviceBroker)
        {
        }

        public async ValueTask<TagHelperResolutionResult> GetTagHelpersAsync(object solutionInfo, ProjectSnapshotHandle projectHandle, string factoryTypeName, CancellationToken cancellationToken = default)
        {
            var solution = await RazorRemoteUtilities.GetSolutionAsync(ServiceBrokerClient, solutionInfo, cancellationToken).ConfigureAwait(false);

            var projectSnapshot = await GetProjectSnapshotAsync(projectHandle, cancellationToken).ConfigureAwait(false);
            var workspaceProject = solution
                .Projects
                .FirstOrDefault(project => FilePathComparer.Instance.Equals(project.FilePath, projectSnapshot.FilePath));

            if (workspaceProject == null)
            {
                return TagHelperResolutionResult.Empty;
            }

            return await RazorServices.TagHelperResolver.GetTagHelpersAsync(workspaceProject, projectHandle.Configuration, factoryTypeName, cancellationToken);
        }
    }
}
