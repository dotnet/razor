// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.CodeAnalysis.Razor.Compiler.Language;

internal partial class SymbolCache
{
    public sealed partial class SymbolData
    {
        private sealed class ToDisplayStringResult
        {
            private readonly ISymbol _symbol;

            private string? _emptyDisplayFormatValue;
            private string? _fullNameTypeDisplayFormatValue;
            private string? _globallyQualifiedFullNameTypeDisplayFormatValue;

            public ToDisplayStringResult(ISymbol symbol)
            {
                _symbol = symbol;
            }

            public string ToDisplayString(SymbolDisplayFormat? format)
            {
                if (format == null)
                {
                    return GetToDisplayStringResult(_symbol, format, ref _emptyDisplayFormatValue);
                }
                else if (format == WellKnownSymbolDisplayFormats.FullNameTypeDisplayFormat)
                {
                    return GetToDisplayStringResult(_symbol, format, ref _fullNameTypeDisplayFormatValue);
                }
                else if (format == WellKnownSymbolDisplayFormats.GloballyQualifiedFullNameTypeDisplayFormat)
                {
                    return GetToDisplayStringResult(_symbol, format, ref _globallyQualifiedFullNameTypeDisplayFormatValue);
                }

                throw new InvalidOperationException("The provided format is not cached. Only the default, FullNameTypeDisplayFormat, and GloballyQualifiedFullNameTypeDisplayFormat formats are cached.");

                static string GetToDisplayStringResult(ISymbol symbol, SymbolDisplayFormat? format, ref string? cachedValue)
                {
                    if (cachedValue == null)
                    {
#pragma warning disable RS0030 // Do not use banned APIs
                        cachedValue = symbol.ToDisplayString(format);
#pragma warning restore RS0030 // Do not use banned APIs
                    }

                    return cachedValue;
                }
            }
        }
    }
}
