﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.ExternalAccess.LegacyEditor;

internal sealed class ContextChangeEventArgs(ContextChangeKind kind) : EventArgs
{
    public ContextChangeKind Kind { get; } = kind;
}
