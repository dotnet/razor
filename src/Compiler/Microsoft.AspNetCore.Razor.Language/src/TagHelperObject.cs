// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

public abstract class TagHelperObject
{
    private int _flags;

    private protected const int ContainsDiagnosticsBit = 1 << 0;

    private protected bool HasFlag(int flag) => (_flags & flag) != 0;
    private protected void SetFlag(int toSet) => ThreadSafeFlagOperations.Set(ref _flags, toSet);
    private protected void ClearFlag(int toClear) => ThreadSafeFlagOperations.Clear(ref _flags, toClear);
    private protected void SetOrClearFlag(int toChange, bool value) => ThreadSafeFlagOperations.SetOrClear(ref _flags, toChange, value);
}
