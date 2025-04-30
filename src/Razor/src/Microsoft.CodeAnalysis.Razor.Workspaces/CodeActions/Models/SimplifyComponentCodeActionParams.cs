// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Models;

internal sealed class SimplifyTagToSelfClosingCodeActionParams
{
    [JsonPropertyName("startTagCloseAngleIndex")]
    public int StartTagCloseAngleIndex { get; set; }

    [JsonPropertyName("endTagCloseAngleIndex")]
    public int EndTagCloseAngleIndex { get; set; }
}
