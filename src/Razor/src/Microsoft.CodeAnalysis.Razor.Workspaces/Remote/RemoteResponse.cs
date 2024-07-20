// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Remote;

[DataContract]
internal record struct RemoteResponse<T>(
    [property: DataMember(Order = 0)] bool ShouldCallHtml,
    [property: DataMember(Order = 1)] T Result)
{
    public static RemoteResponse<T> CallHtml => new(ShouldCallHtml: true, Result: default!);
    public static RemoteResponse<T> NoFurtherHandling => new(ShouldCallHtml: false, Result: default!);
    public static RemoteResponse<T> Results(T result) => new(ShouldCallHtml: false, Result: result);
}
