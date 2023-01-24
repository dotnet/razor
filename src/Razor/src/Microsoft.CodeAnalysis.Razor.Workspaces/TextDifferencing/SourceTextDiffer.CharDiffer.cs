// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.TextDifferencing;

internal partial class SourceTextDiffer
{
    private class CharDiffer : SourceTextDiffer
    {
        public override int OldSourceLength { get; }
        public override int NewSourceLength { get; }

        private char[] _appendBuffer;

        public CharDiffer(SourceText oldText, SourceText newText)
            : base(oldText, newText)
        {
            _appendBuffer = Rent(1024);

            OldSourceLength = oldText.Length;
            NewSourceLength = newText.Length;
        }

        public override void Dispose()
        {
            Return(_appendBuffer);
        }

        public override bool SourceEqual(int oldSourceIndex, int newSourceIndex)
            => OldText[oldSourceIndex] == NewText[newSourceIndex];

        protected override int GetEditPosition(DiffEdit edit)
            => edit.Position;

        protected override int AppendEdit(DiffEdit edit, StringBuilder builder)
        {
            if (edit.Kind == DiffEditKind.Insert)
            {
                Assumes.NotNull(edit.NewTextPosition);
                var newTextPosition = edit.NewTextPosition.GetValueOrDefault();

                if (edit.Length > 1)
                {
                    var buffer = EnsureBuffer(ref _appendBuffer, edit.Length);
                    NewText.CopyTo(newTextPosition, buffer, 0, edit.Length);

                    builder.Append(buffer, 0, edit.Length);
                }
                else if (edit.Length == 1)
                {
                    builder.Append(NewText[newTextPosition]);
                }

                return edit.Position;
            }

            return edit.Position + edit.Length;
        }
    }
}
