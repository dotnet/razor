// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Razor;
using System.Diagnostics;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal sealed class CSharpDocumentExcerptService : DocumentExcerptServiceBase
    {
        private readonly IRazorSpanMappingService _mappingService;

        private readonly ITextSnapshot _textSnapshot;
        private readonly LSPDocumentSnapshot _documentSnapshot;

        public CSharpDocumentExcerptService(
            IRazorSpanMappingService mappingService,
            LSPDocumentSnapshot documentSnapshot,
            ITextSnapshot textSnapshot)
        {
            if (mappingService is null)
            {
                throw new ArgumentNullException(nameof(mappingService));
            }

            if (textSnapshot == null)
            {
                throw new ArgumentNullException(nameof(textSnapshot));
            }

            if (documentSnapshot is null)
            {
                throw new ArgumentNullException(nameof(documentSnapshot));
            }

            _mappingService = mappingService;

            _textSnapshot = textSnapshot;
            _documentSnapshot = documentSnapshot;
        }

        internal override async Task<ExcerptResultInternal?> TryGetExcerptInternalAsync(
            Document document,
            TextSpan span,
            ExcerptModeInternal mode,
            CancellationToken cancellationToken)
        {
            if (_textSnapshot == null)
            {
                return null;
            }

            var mapped = await _mappingService.MapSpansAsync(document, new[] { span }, cancellationToken).ConfigureAwait(false);
            if (mapped.Length == 0 || mapped[0].Equals(default(RazorMappedSpanResult)))
            {
                return null;
            }

            var razorDocumentText = _documentSnapshot.Snapshot.AsText();
            var razorDocumentSpan = razorDocumentText.Lines.GetTextSpan(mapped[0].LinePositionSpan);

            var generatedDocument = document;

            // First compute the range of text we want to we to display relative to the primary document.
            var excerptSpan = ChooseExcerptSpan(razorDocumentText, razorDocumentSpan, mode);

            // Then we'll classify the spans based on the primary document, since that's the coordinate
            // space that our output mappings use.
            var classifiedSpans = await ClassifyPreviewAsync(
                razorDocumentSpan,
                excerptSpan,
                generatedDocument,
                cancellationToken).ConfigureAwait(false);

            // Now translate everything to be relative to the excerpt
            var offset = 0 - excerptSpan.Start;
            var excerptText = razorDocumentText.GetSubText(excerptSpan);
            excerptSpan = new TextSpan(0, excerptSpan.Length);
            razorDocumentSpan = new TextSpan(razorDocumentSpan.Start + offset, razorDocumentSpan.Length);

            for (var i = 0; i < classifiedSpans.Count; i++)
            {
                var classifiedSpan = classifiedSpans[i];
                var updated = new TextSpan(classifiedSpan.TextSpan.Start + offset, classifiedSpan.TextSpan.Length);
                Debug.Assert(excerptSpan.Contains(updated));

                classifiedSpans[i] = new ClassifiedSpan(classifiedSpan.ClassificationType, updated);
            }

            return new ExcerptResultInternal(excerptText, razorDocumentSpan, classifiedSpans.ToImmutable(), document, span);
        }

        private async Task<ImmutableArray<ClassifiedSpan>.Builder> ClassifyPreviewAsync(
            TextSpan primarySpan,
            TextSpan excerptSpan,
            Document generatedDocument,
            CancellationToken cancellationToken)
        {
            var builder = ImmutableArray.CreateBuilder<ClassifiedSpan>();

            var remainingSpan = excerptSpan;
            var intersection = primarySpan.Intersection(remainingSpan);
            Debug.Assert(intersection != null);

            // OK this span intersects with the excerpt span, so we will process it. Let's compute
            // the secondary span that matches the intersection.
            var secondarySpan = new TextSpan(primarySpan.Start + intersection.Value.Start - primarySpan.Start, intersection.Value.Length);

            if (remainingSpan.Start < primarySpan.Start)
            {
                // The position is before the next C# span. Classify everything up to the C# start
                // as text.
                builder.Add(new ClassifiedSpan(ClassificationTypeNames.Text, new TextSpan(remainingSpan.Start, primarySpan.Start - remainingSpan.Start)));

                // Advance to the start of the C# span.
                remainingSpan = new TextSpan(primarySpan.Start, remainingSpan.Length - (primarySpan.Start - remainingSpan.Start));
            }

            var suffix = new ClassifiedSpan();
            if (remainingSpan.End > primarySpan.End)
            {
                suffix = new ClassifiedSpan(ClassificationTypeNames.Text, new TextSpan(primarySpan.End, remainingSpan.End - primarySpan.End));

                // We've taken out the prefix and suffix from the excerpt span
                remainingSpan = primarySpan;
            }

            // We should be able to process this whole span as C#, so classify it.
            //
            // However, we'll have to translate it to the the generated document's coordinates to do that.
            var classifiedSecondarySpans = await Classifier.GetClassifiedSpansAsync(
                generatedDocument,
                remainingSpan,
                cancellationToken);

            // NOTE: The Classifier will only returns spans for things that it understands. That means
            // that whitespace is not classified. The preview expects us to provide contiguous spans, 
            // so we are going to have to fill in the gaps.

            // Now we have to translate back to the primary document's coordinates.
            var offset = primarySpan.Start - secondarySpan.Start;
            foreach (var classifiedSecondarySpan in classifiedSecondarySpans)
            {
                var updated = classifiedSecondarySpan.TextSpan.Contains(secondarySpan) ?
                    new TextSpan(secondarySpan.Start + offset, secondarySpan.Length) :
                    new TextSpan(classifiedSecondarySpan.TextSpan.Start + offset, classifiedSecondarySpan.TextSpan.Length);

                // Make sure that we're not introducing a gap. Remember, we need to fill in the whitespace.
                if (remainingSpan.Start < updated.Start)
                {
                    builder.Add(new ClassifiedSpan(
                        ClassificationTypeNames.Text,
                        new TextSpan(remainingSpan.Start, updated.Start - remainingSpan.Start)));
                    remainingSpan = new TextSpan(updated.Start, remainingSpan.Length - (updated.Start - remainingSpan.Start));
                }

                builder.Add(new ClassifiedSpan(classifiedSecondarySpan.ClassificationType, updated));
                remainingSpan = new TextSpan(updated.End, remainingSpan.Length - (updated.End - remainingSpan.Start));
            }

            // Make sure that we're not introducing a gap. Remember, we need to fill in the whitespace.
            if (remainingSpan.Start < primarySpan.End)
            {
                builder.Add(new ClassifiedSpan(
                    ClassificationTypeNames.Text,
                    new TextSpan(remainingSpan.Start, primarySpan.End - remainingSpan.Start)));
                remainingSpan = new TextSpan(primarySpan.End, remainingSpan.Length - (primarySpan.End - remainingSpan.Start));
            }

            // Deal with residue
            if (remainingSpan.Length > 0)
            {
                // Trailing Razor/markup text.
                builder.Add(new ClassifiedSpan(ClassificationTypeNames.Text, remainingSpan));
            }

            builder.Add(suffix);

            return builder;
        }
    }
}
