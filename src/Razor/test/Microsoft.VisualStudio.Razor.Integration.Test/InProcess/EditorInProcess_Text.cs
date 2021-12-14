// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Xunit;

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

        public async Task VerifyGetClassificationsAsync(IEnumerable<ClassificationSpan> expectedClassifications, CancellationToken cancellationToken)
        {
            var actualClassifications = await GetClassificationsAsync(cancellationToken);
            var actualArray = actualClassifications.ToArray();
            var expectedArray = expectedClassifications.ToArray();

            Assert.Equal(expectedArray.Length, actualArray.Length);
            for (var i = 0; i < actualArray.Length; i++)
            {
                var actualClassification = actualArray[i];
                var expectedClassification = expectedArray[i];

                if (!expectedClassification.Span.Span.Equals(actualClassification.Span.Span)
                    || !string.Equals(expectedClassification.ClassificationType.Classification, actualClassification.ClassificationType.Classification))
                {

                    Assert.Equal(expectedClassification.Span, actualClassification.Span);
                    Assert.Equal(expectedClassification.ClassificationType.Classification, actualClassification.ClassificationType.Classification);

                    Assert.True(false,
                        $"i: {i}" +
                        $"expected: {expectedClassification.Span} {expectedClassification.ClassificationType.Classification} " +
                        $"actual: {actualClassification.Span} {actualClassification.ClassificationType.Classification}");
                }
            }
        }

        public async Task<IEnumerable<ClassificationSpan>> GetClassificationsAsync(CancellationToken cancellationToken)
        {
            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.LanguageServer, cancellationToken);
            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.Workspace, cancellationToken);

            var textView = await TestServices.Editor.GetActiveTextViewAsync(cancellationToken);
            var selectionSpan = textView.Selection.StreamSelectionSpan.SnapshotSpan;
            Assert.Equal(0, selectionSpan.Length);
            if (selectionSpan.Length == 0)
            {
                selectionSpan = new SnapshotSpan(textView.TextSnapshot, new Span(0, textView.TextSnapshot.Length));
            }

            var classifierService = GetComponentModelService<IViewClassifierAggregatorService>(cancellationToken);
            var classifier = classifierService.GetClassifier(textView);
            var classifiedSpans = classifier.GetClassificationSpans(selectionSpan);
            return classifiedSpans;
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
