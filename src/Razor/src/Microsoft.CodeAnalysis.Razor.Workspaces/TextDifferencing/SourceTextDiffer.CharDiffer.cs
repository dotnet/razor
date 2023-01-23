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

        public CharDiffer(SourceText oldText, SourceText newText)
            : base(oldText, newText)
        {
            OldSourceLength = oldText.Length;
            NewSourceLength = newText.Length;
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

                builder.Append(NewText[edit.NewTextPosition.GetValueOrDefault()]);
                return edit.Position;
            }

            return edit.Position + 1;
        }
    }
}
