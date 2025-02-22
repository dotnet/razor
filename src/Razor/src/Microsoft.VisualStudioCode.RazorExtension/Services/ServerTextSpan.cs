// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

internal sealed class ServerTextSpan
{
    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }

    public TextSpan ToTextSpan()
        => new(Start, Length);
}
