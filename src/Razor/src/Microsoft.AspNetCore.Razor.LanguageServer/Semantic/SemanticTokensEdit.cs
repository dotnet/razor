// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    public class SemanticTokensEdit
    {
        public int Start { get; set; }
        public int DeleteCount { get; set; }
        public IEnumerable<uint> Data { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as SemanticTokensEdit;
            if (other is null)
            {
                return false;
            }

            if (Data is null || other.Data is null)
            {
                return Data is null && other.Data is null;
            }

            return Enumerable.SequenceEqual(Data, other.Data)
                && DeleteCount.Equals(other.DeleteCount)
                && Start.Equals(other.Start);
        }

        public override int GetHashCode()
        {
            var combiner = HashCodeCombiner.Start();

            combiner.Add(Start);
            combiner.Add(DeleteCount);
            combiner.Add(Data);

            return combiner.CombinedHash;
        }
    }
}
