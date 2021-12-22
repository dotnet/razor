// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Xunit.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.Razor.Integration.Test.InProcess
{
    internal abstract class InProcComponent
    {
        protected InProcComponent(TestServices testServices)
        {
            TestServices = testServices ?? throw new ArgumentNullException(nameof(testServices));
        }

        public TestServices TestServices { get; }

        protected JoinableTaskFactory JoinableTaskFactory => TestServices.JoinableTaskFactory;

        protected async Task<TInterface> GetRequiredGlobalServiceAsync<TService, TInterface>(CancellationToken cancellationToken)
            where TService : class
            where TInterface : class
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var serviceProvider = (IAsyncServiceProvider2?)await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SAsyncServiceProvider)).WithCancellation(cancellationToken);
            Assumes.Present(serviceProvider);

            var @interface = (TInterface?)await serviceProvider.GetServiceAsync(typeof(TService)).WithCancellation(cancellationToken);
            Assumes.Present(@interface);
            return @interface;
        }

        protected async Task<TService> GetComponentModelServiceAsync<TService>(CancellationToken cancellationToken)
            where TService : class
        {
            var componentModel = await GetRequiredGlobalServiceAsync<SComponentModel, IComponentModel>(cancellationToken);
            return componentModel.GetService<TService>();
        }

        protected async Task ExecuteCommandAsync(string commandName, CancellationToken cancellationToken, string args = "")
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);
            dte.ExecuteCommand(commandName, args);
        }

        /// <summary>
        /// Waiting for the application to 'idle' means that it is done pumping messages (including WM_PAINT).
        /// </summary>
        /// <param name="cancellationToken">The cancellation token that the operation will observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected static async Task WaitForApplicationIdleAsync(CancellationToken cancellationToken)
        {
            var synchronizationContext = new DispatcherSynchronizationContext(Application.Current.Dispatcher, DispatcherPriority.ApplicationIdle);
            var taskScheduler = new SynchronizationContextTaskScheduler(synchronizationContext);
            await Task.Factory.StartNew(
                () => { },
                cancellationToken,
                TaskCreationOptions.None,
                taskScheduler);
        }
    }
}
