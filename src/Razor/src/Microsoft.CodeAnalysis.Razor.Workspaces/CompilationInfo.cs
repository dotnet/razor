// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

/// <summary>
/// Information that we need from a Roslyn compilation
/// </summary>
[DataContract]
internal readonly record struct CompilationInfo(
    [property: DataMember(Order = 0)]
    bool HasAddComponentParameter);
