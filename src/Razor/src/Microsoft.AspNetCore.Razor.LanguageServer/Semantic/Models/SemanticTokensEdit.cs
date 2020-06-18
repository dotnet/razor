// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
    internal class SemanticTokensEdit
    {
        public int Start { get; set; }
        public int DeleteCount { get; set; }
        public IEnumerable<uint> Data { get; set; }
    }
}
