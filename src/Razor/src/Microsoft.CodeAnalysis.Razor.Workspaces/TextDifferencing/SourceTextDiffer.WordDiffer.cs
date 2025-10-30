// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.TextDifferencing;

internal partial class SourceTextDiffer
{
    private sealed class WordDiffer : SourceTextDiffer
    {
        private readonly ImmutableArray<TextSpan> _oldWords;
        private readonly ImmutableArray<TextSpan> _newWords;

        private char[] _oldBuffer;
        private char[] _newBuffer;
        private char[] _appendBuffer;

        protected override int OldSourceLength { get; }
        protected override int NewSourceLength { get; }

        public WordDiffer(SourceText oldText, SourceText newText) : base(oldText, newText)
        {
            _oldBuffer = RentArray(1024);
            _newBuffer = RentArray(1024);
            _appendBuffer = RentArray(1024);

            _oldWords = TokenizeWords(oldText);
            _newWords = TokenizeWords(newText);

            OldSourceLength = _oldWords.Length;
            NewSourceLength = _newWords.Length;
        }

        public override void Dispose()
        {
            ReturnArray(_oldBuffer);
            ReturnArray(_newBuffer);
            ReturnArray(_appendBuffer);
        }

        private static ImmutableArray<TextSpan> TokenizeWords(SourceText text)
        {
            if (text.Length == 0)
            {
                return [];
            }

            using var builder = new PooledArrayBuilder<TextSpan>();

            var currentSpanStart = 0;
            var currentClassification = Classify(text[0]);

            // This algorithm is simpler than a normal tokenizer might be because we want to keep contiguous
            // whitespace characters in the same "word", and we don't really care about contiguous quotes
            // or slashes, so we can keep it simple and just capture a "word" when the classification of
            // the current character changes.
            var index = 1;
            while (index < text.Length)
            {
                var classification = Classify(text[index]);
                if (classification != currentClassification)
                {
                    // We've hit a word boundary, so store this and move on
                    builder.Add(TextSpan.FromBounds(currentSpanStart, index));
                    currentSpanStart = index;
                    currentClassification = classification;
                }

                index++;
            }

            // It's impossible for the loop to capture the last word
            Debug.Assert(currentSpanStart < text.Length);
            builder.Add(TextSpan.FromBounds(currentSpanStart, text.Length));

            return builder.ToImmutableAndClear();

            // The type of classification doesn't matter as long as its unique and equatible
            static int Classify(char c)
                => c switch
                {
                    '/' => 0,
                    '"' => 1,
                    _ when char.IsWhiteSpace(c) => 2,
                    _ => 3,
                };
        }

        protected override bool SourceEqual(int oldSourceIndex, int newSourceIndex)
        {
            var oldWord = _oldWords[oldSourceIndex];
            var newWord = _newWords[newSourceIndex];
            if (oldWord.Length != newWord.Length)
            {
                return false;
            }

            var length = oldWord.Length;

            // Copy the text into char arrays for comparison. Note: To avoid allocation,
            // we try to reuse the same char buffers and only grow them when a longer
            // line is encountered.
            var oldChars = EnsureBuffer(ref _oldBuffer, oldWord.Length);
            var newChars = EnsureBuffer(ref _newBuffer, newWord.Length);

            OldText.CopyTo(oldWord.Start, oldChars, 0, length);
            NewText.CopyTo(newWord.Start, newChars, 0, length);

            for (var i = 0; i < length; i++)
            {
                if (oldChars[i] != newChars[i])
                {
                    return false;
                }
            }

            return true;
        }

        protected override int GetEditPosition(DiffEdit edit)
            => _oldWords[edit.Position].Start;

        protected override int AppendEdit(DiffEdit edit, StringBuilder builder)
        {
            if (edit.Kind == DiffEditKind.Insert)
            {
                Assumes.NotNull(edit.NewTextPosition);
                var newWordIndex = edit.NewTextPosition.GetValueOrDefault();

                for (var i = 0; i < edit.Length; i++)
                {
                    var word = _newWords[newWordIndex + i];
                    var buffer = EnsureBuffer(ref _appendBuffer, word.Length);
                    NewText.CopyTo(word.Start, buffer, 0, word.Length);

                    builder.Append(buffer, 0, word.Length);
                }

                return _oldWords[edit.Position].Start;
            }

            return _oldWords[edit.Position + edit.Length - 1].End;
        }
    }
}
