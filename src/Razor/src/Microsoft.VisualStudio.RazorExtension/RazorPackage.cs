// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServerClient.Razor;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Debugging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.RazorExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [AboutDialogInfo(PackageGuidString, "Razor (ASP.NET Core)", "#110", "#112", IconResourceID = "#400")]
    [ProvideService(typeof(RazorLanguageService))]
    [ProvideLanguageService(typeof(RazorLanguageService), RazorConstants.RazorLSPContentTypeName, 110)]
    [ProvideBrokeredServiceHubService("Microsoft.VisualStudio.Razor.TagHelperProvider", Audience = ServiceAudience.Local)]
    [ProvideBrokeredServiceHubService("Microsoft.VisualStudio.Razor.TagHelperProvider64", Audience = ServiceAudience.Local)]
    [ProvideBrokeredServiceHubService("Microsoft.VisualStudio.Razor.TagHelperProvider64S", Audience = ServiceAudience.Local)]
    [ProvideBrokeredServiceHubService("Microsoft.VisualStudio.Razor.TagHelperProviderCore64", ServiceLocation = ProvideBrokeredServiceHubServiceAttribute.DefaultServiceLocation + @"\ServiceHubCore", Audience = ServiceAudience.Local)]
    [ProvideBrokeredServiceHubService("Microsoft.VisualStudio.Razor.TagHelperProviderCore64S", ServiceLocation = ProvideBrokeredServiceHubServiceAttribute.DefaultServiceLocation + @"\ServiceHubCore", Audience = ServiceAudience.Local)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuidString)]
    public sealed class RazorPackage : AsyncPackage
    {
        public const string PackageGuidString = "13b72f58-279e-49e0-a56d-296be02f0805";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var container = this as IServiceContainer;
            container.AddService(typeof(RazorLanguageService), (container, type) =>
            {
                var componentModel = (IComponentModel)GetGlobalService(typeof(SComponentModel));
                var breakpointResolver = componentModel.GetService<RazorBreakpointResolver>();
                var proximityExpressionResolver = componentModel.GetService<RazorProximityExpressionResolver>();
                var uiThreadOperationExecutor = componentModel.GetService<IUIThreadOperationExecutor>();
                var editorAdaptersFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
                var joinableTaskContext = componentModel.GetService<JoinableTaskContext>();

                return new RazorLanguageService(breakpointResolver, proximityExpressionResolver, uiThreadOperationExecutor, editorAdaptersFactory, joinableTaskContext.Factory);
            }, promote: true);
        }
    }
}
