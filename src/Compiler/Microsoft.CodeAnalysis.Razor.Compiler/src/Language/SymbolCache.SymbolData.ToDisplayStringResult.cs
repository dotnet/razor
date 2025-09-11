// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                else if (format == SymbolExtensions.FullNameTypeDisplayFormat)
                {
                    return GetToDisplayStringResult(_symbol, format, ref _fullNameTypeDisplayFormatValue);
                }
                else if (format == ComponentTagHelperDescriptorProvider.GloballyQualifiedFullNameTypeDisplayFormat)
                {
                    return GetToDisplayStringResult(_symbol, format, ref _globallyQualifiedFullNameTypeDisplayFormatValue);
                }

                return _symbol.ToDisplayString(format);

                static string GetToDisplayStringResult(ISymbol symbol, SymbolDisplayFormat? format, ref string? cachedValue)
                {
                    if (cachedValue == null)
                    {
                        cachedValue = symbol.ToDisplayString(format);
                    }

                    return cachedValue;
                }
            }
        }
    }
}
