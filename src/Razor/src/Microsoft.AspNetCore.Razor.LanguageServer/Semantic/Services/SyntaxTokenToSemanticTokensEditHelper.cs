// Copyright(c).NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Services
{
    internal static class SyntaxTokenToSemanticTokensEditHelper
    {
        // The below algorithm was taken from OmniSharp/csharp-language-server-protocol at
        // https://github.com/OmniSharp/csharp-language-server-protocol/blob/bdec4c73240be52fbb25a81f6ad7d409f77b5215/src/Protocol/Document/Proposals/SemanticTokensDocument.cs#L156
        public static SemanticTokensOrSemanticTokensEdits ConvertSyntaxTokensToSemanticEdits(
            SemanticTokens newTokens,
            IReadOnlyList<uint> previousResults)
        {
            var diffs = UIntArrayDiffer.GetEdits(previousResults.ToArray(), newTokens.Data);
            var edits = ComputeSemanticTokenEdits(diffs, newTokens.Data);
            var result = new SemanticTokensEditCollection
            {
                ResultId = newTokens.ResultId,
                Edits = edits,
            };

            return result;
        }

        private static IReadOnlyList<SemanticTokensEdit> ComputeSemanticTokenEdits(IReadOnlyList<DiffEdit> diffs, uint[] newArray)
        {
            var results = new List<SemanticTokensEdit>();
            foreach (var diff in diffs)
            {
                var current = results.Any() ? results.Last() : null;
                switch (diff.Operation)
                {
                    case DiffEdit.Type.Delete:
                        if (current != null
                            && current.DeleteCount > 0
                            && current.Start + current.DeleteCount == diff.Position)
                        {
                            current.DeleteCount += 1;
                        }
                        else
                        {
                            results.Add(new SemanticTokensEdit
                            {
                                Start = diff.Position,
                                Data = new uint[] { },
                                DeleteCount = 1,
                            });
                        }
                        break;
                    case DiffEdit.Type.Insert:
                        if (current != null
                            && current.Data.Count() > 0
                            && current.Start == diff.Position)
                        {
                            current.Data = current.Data.Append(newArray[diff.NewTextPosition.Value]);
                        }
                        else
                        {
                            results.Add(new SemanticTokensEdit
                            {
                                Start = diff.Position,
                                Data = new uint[] { newArray[diff.NewTextPosition.Value] },
                                DeleteCount = 0,
                            });
                        }
                        break;
                }
            }

            return results;
        }
    }

    internal class UIntArrayDiffer : TextDiffer
    {
        private UIntArrayDiffer(uint[] oldArray, uint[] newArray)
        {
            OldArray = oldArray;
            NewArray = newArray;
        }

        private uint[] OldArray { get; }
        private uint[] NewArray { get; }

        protected override int OldTextLength => OldArray.Count();
        protected override int NewTextLength => NewArray.Count();

        protected override bool ContentEquals(int oldTextIndex, int newTextIndex)
        {
            return OldArray[oldTextIndex] == NewArray[newTextIndex];
        }

        public static IReadOnlyList<DiffEdit> GetEdits(uint[] oldArray, uint[] newArray)
        {
            if (oldArray is null)
            {
                throw new ArgumentNullException();
            }
            if (newArray is null)
            {
                throw new ArgumentNullException();
            }

            if (oldArray.SequenceEqual(newArray))
            {
                return Array.Empty<DiffEdit>();
            }
            else if (oldArray.Length == 0 || newArray.Length == 0)
            {
                throw new NotImplementedException();
            }

            var differ = new UIntArrayDiffer(oldArray, newArray);
            var diffs = differ.ComputeDiff();

            return diffs;
        }
    }
}
