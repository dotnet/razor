// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Folding
{
    internal record RazorFoldingRangeResponse(ICollection<FoldingRange> HtmlRanges, ICollection<FoldingRange> CSharpRanges);
}
