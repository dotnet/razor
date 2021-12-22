// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Microsoft.VisualStudio.Razor.Integration.Test.InProcess
{
    internal partial class EditorInProcess
    {
        /// <summary>
        /// Waits for any semantic classifications to be available on the active TextView, and for at least one of the
        /// <paramref name="expectedClassification"/> if provided.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="expectedClassification">The classification to wait for, if any.</param>
        /// <returns>A <see cref="Task"/> which completes when classification is "ready".</returns>
        public async Task WaitForClassificationAsync(CancellationToken cancellationToken, string? expectedClassification = null)
        {
            var textView = await TestServices.Editor.GetActiveTextViewAsync(cancellationToken);
            var classifier = await GetClassifierAsync(textView, cancellationToken);

            using var semaphore = new SemaphoreSlim(1);
            await semaphore.WaitAsync(cancellationToken);

            classifier.ClassificationChanged += Classifier_ClassificationChanged;

            // Check that we're not ALREADY changed
            if (await HasClassificationAsync(cancellationToken))
            {
                semaphore.Release();
                classifier.ClassificationChanged -= Classifier_ClassificationChanged;
                return;
            }

            try
            {
                await semaphore.WaitAsync(cancellationToken);
            }
            finally
            {
                classifier.ClassificationChanged -= Classifier_ClassificationChanged;
            }

            async void Classifier_ClassificationChanged(object sender, ClassificationChangedEventArgs e)
            {
                var classifications = await GetClassificationsAsync(cancellationToken);

                if (await HasClassificationAsync(cancellationToken))
                {
                    semaphore.Release();
                }
            }

            async Task<bool> HasClassificationAsync(CancellationToken cancellationToken)
            {
                var classifications = await GetClassificationsAsync(cancellationToken);
                return classifications.Any(
                    c => c.ClassificationType.BaseTypes.Any(bT => bT is ILayeredClassificationType layered &&
                        layered.Layer == ClassificationLayer.Semantic &&
                        (expectedClassification is null || layered.Classification == expectedClassification)));
            }
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

                if (actualClassification.ClassificationType.BaseTypes.Count() > 1)
                {
                    foreach (var baseType in actualClassification.ClassificationType.BaseTypes)
                    {
                        if (baseType is ILayeredClassificationType layeredClassification &&
                            layeredClassification.Layer == ClassificationLayer.Semantic)
                        {
                            Assert.Equal(expectedClassification.Span, actualClassification.Span);
                            Assert.Equal(expectedClassification.ClassificationType.Classification, layeredClassification.Classification);
                        }
                    }
                }
                else if (!expectedClassification.Span.Span.Equals(actualClassification.Span.Span)
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
            await WaitForProjectReadyAsync(cancellationToken);
            var textView = await TestServices.Editor.GetActiveTextViewAsync(cancellationToken);

            var selectionSpan = textView.Selection.StreamSelectionSpan.SnapshotSpan;
            Assert.Equal(0, selectionSpan.Length);
            if (selectionSpan.Length == 0)
            {
                selectionSpan = new SnapshotSpan(textView.TextSnapshot, new Span(0, textView.TextSnapshot.Length));
            }

            var classifier = await GetClassifierAsync(textView, cancellationToken);
            var classifiedSpans = classifier.GetClassificationSpans(selectionSpan);
            return classifiedSpans;
        }

        private async Task<IClassifier> GetClassifierAsync(IWpfTextView textView, CancellationToken cancellationToken)
        {
            var classifierService = await GetComponentModelServiceAsync<IViewClassifierAggregatorService>(cancellationToken);

            return classifierService.GetClassifier(textView);
        }

    }
}
