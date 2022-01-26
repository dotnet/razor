// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Razor.IntegrationTests.InProcess;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Microsoft.VisualStudio.Extensibility.Testing
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

        public async Task VerifyTextContainsAsync(string text, CancellationToken cancellationToken)
        {
            var view = await GetActiveTextViewAsync(cancellationToken);
            var content = view.TextBuffer.CurrentSnapshot.GetText();
            Assert.Contains(text, content);
        }

        public async Task WaitForCurrentLineTextAsync(string text, CancellationToken cancellationToken)
        {
            var view = await GetActiveTextViewAsync(cancellationToken);

            using var semaphore = new SemaphoreSlim(1);
            await semaphore.WaitAsync(cancellationToken);

            var caret = view.Caret.Position.BufferPosition;
            var line = view.TextBuffer.CurrentSnapshot.GetLineFromPosition(caret).GetText();
            if (line.Trim() == text.Trim())
            {
                semaphore.Release();
                view.Caret.PositionChanged -= Caret_PositionChanged;
                return;
            }

            view.Caret.PositionChanged += Caret_PositionChanged;

            try
            {
                await semaphore.WaitAsync(cancellationToken);
            }
            finally
            {
                view.Caret.PositionChanged -= Caret_PositionChanged;
            }

            void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e)
            {
                var caret = view.Caret.Position.BufferPosition;
                var line = view.TextBuffer.CurrentSnapshot.GetLineFromPosition(caret).GetText();
                if (line.Trim() == text.Trim())
                {
                    semaphore.Release();
                }
            }
        }

        public async Task WaitForActiveWindowAsync(string windowTitle, CancellationToken cancellationToken)
        {
            await Helper.RetryAsync(async ct =>
                {
                    var dte = await GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);

                    return dte.ActiveWindow.Caption == windowTitle;
                },
                TimeSpan.FromMilliseconds(50),
                cancellationToken);
        }

        public async Task WaitForProjectReadyAsync(CancellationToken cancellationToken)
        {
            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.LanguageServer, cancellationToken);
            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.Workspace, cancellationToken);
        }
    }
}
