// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language;

public abstract class TagHelperObject
{
    private int _flags;
    public ImmutableArray<RazorDiagnostic> Diagnostics { get; }

    private protected const int CaseSensitiveBit = 1 << 0;
    private protected const int LastFlagBit = CaseSensitiveBit;

    public bool HasErrors
        => Diagnostics.Any(static d => d.Severity == RazorDiagnosticSeverity.Error);

    private protected TagHelperObject(ImmutableArray<RazorDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics.NullToEmpty();
    }

    private protected bool HasFlag(int flag) => (_flags & flag) != 0;
    private protected void SetFlag(int toSet) => ThreadSafeFlagOperations.Set(ref _flags, toSet);
    private protected void ClearFlag(int toClear) => ThreadSafeFlagOperations.Clear(ref _flags, toClear);
    private protected void SetOrClearFlag(int toChange, bool value) => ThreadSafeFlagOperations.SetOrClear(ref _flags, toChange, value);

    private protected bool? GetTriStateFlags(int isSetFlag, int isOnFlag)
    {
        var flags = _flags;

        if ((flags & isSetFlag) == 0)
        {
            return null;
        }

        return (flags & isOnFlag) != 0;
    }

    private protected void UpdateTriStateFlags(bool? value, int isSetFlag, int isOnFlag)
    {
        switch (value)
        {
            case true:
                SetFlag(isSetFlag | isOnFlag);
                break;

            case false:
                ClearFlag(isOnFlag);
                SetFlag(isSetFlag);
                break;

            case null:
                ClearFlag(isSetFlag);
                break;
        }
    }
}
