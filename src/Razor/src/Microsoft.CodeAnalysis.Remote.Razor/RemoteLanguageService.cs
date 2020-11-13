// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.ServiceHub.Framework;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Remote;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    internal sealed class RemoteLanguageService : RazorServiceBase, IRemoteLanguageService
    {
        internal sealed class Factory : FactoryBase<IRemoteLanguageService>
        {
            protected override IRemoteLanguageService CreateService(IServiceProvider serviceProvider, IServiceBroker serviceBroker)
                => new RemoteLanguageService(serviceProvider, serviceBroker);
        }

        private RemoteLanguageService(IServiceProvider serviceProvider, IServiceBroker serviceBroker)
            : base(serviceProvider, serviceBroker)
        {
        }

        public async ValueTask<TagHelperResolutionResult> GetTagHelpersAsync(object solutionInfo, ProjectSnapshotHandle projectHandle, string factoryTypeName, CancellationToken cancellationToken = default)
        {
            var solution = await RazorRemoteUtilities.GetSolutionAsync(ServiceBrokerClient, ServiceProvider, solutionInfo, cancellationToken).ConfigureAwait(false);
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
