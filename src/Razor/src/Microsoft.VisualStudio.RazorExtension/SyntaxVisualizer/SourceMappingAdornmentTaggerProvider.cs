// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.LanguageServerClient.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.RazorExtension.SyntaxVisualizer
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(RazorLSPConstants.RazorLSPContentTypeName)]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class SourceMappingAdornmentTaggerProvider : IViewTaggerProvider
    {
        private readonly IBufferTagAggregatorFactoryService _bufferTagAggregatorFactoryService;
        private readonly RazorCodeDocumentProvidingSnapshotChangeTrigger _sourceMappingProjectChangeTrigger;

        [ImportingConstructor]
        public SourceMappingAdornmentTaggerProvider(IBufferTagAggregatorFactoryService bufferTagAggregatorFactoryService, RazorCodeDocumentProvidingSnapshotChangeTrigger sourceMappingProjectChangeTrigger)
        {
            _bufferTagAggregatorFactoryService = bufferTagAggregatorFactoryService;
            _sourceMappingProjectChangeTrigger = sourceMappingProjectChangeTrigger;
        }

        public ITagger<T>? CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null)
                throw new ArgumentNullException("textView");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (buffer != textView.TextBuffer)
                return null;

            return SourceMappingAdornmentTagger.GetTagger(
                (IWpfTextView)textView,
                new Lazy<ITagAggregator<SourceMappingTag>>(
                    () => _bufferTagAggregatorFactoryService.CreateTagAggregator<SourceMappingTag>(textView.TextBuffer)),
                _sourceMappingProjectChangeTrigger)
                as ITagger<T>;
        }
    }
}
