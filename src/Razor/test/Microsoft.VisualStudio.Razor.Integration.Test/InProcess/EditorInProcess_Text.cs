// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Razor.Integration.Test.InProcess
{
    internal partial class EditorInProcess
    {
        public async Task SetTextAsync(string text, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var view = await GetActiveTextViewAsync(cancellationToken);
            var textSnapshot = view.TextSnapshot;
            var replacementSpan = new SnapshotSpan(textSnapshot, 0, textSnapshot.Length);
            view.TextBuffer.Replace(replacementSpan, text);
        }

        private async Task WaitForProjectReadyAsync(CancellationToken cancellationToken)
        {
            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.LanguageServer, cancellationToken);
            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.Workspace, cancellationToken);
        }

        protected TService GetComponentModelService<TService>(CancellationToken cancellationToken)
            where TService : class
        => TestServices.InvokeOnUIThread(cancellationToken => GetComponentModel(cancellationToken).GetService<TService>(), cancellationToken);

        protected IComponentModel GetComponentModel(CancellationToken cancellationToken)
            => GetGlobalService<SComponentModel, IComponentModel>(cancellationToken);

        protected TInterface GetGlobalService<TService, TInterface>(CancellationToken cancellationToken)
            where TService : class
            where TInterface : class
        => TestServices.InvokeOnUIThread(cancellationToken => (TInterface)ServiceProvider.GlobalProvider.GetService(typeof(TService)), cancellationToken);
    }
}
