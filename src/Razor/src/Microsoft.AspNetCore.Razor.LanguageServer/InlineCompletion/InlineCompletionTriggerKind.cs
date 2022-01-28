// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Runtime.Serialization;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

/// <summary>
/// Corresponds to https://devdiv.visualstudio.com/DevDiv/_git/VSLanguageServerClient?path=/src/product/Protocol/LanguageServer.Protocol.Internal/VSInternalInlineCompletionTriggerKind.cs
/// </summary>
[DataContract]
internal enum InlineCompletionTriggerKind
{
    Automatic = 0,

    Explicit = 1,
}
