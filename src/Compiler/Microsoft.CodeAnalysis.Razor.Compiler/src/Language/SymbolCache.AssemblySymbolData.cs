﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Compiler.Language;

internal partial class SymbolCache
{
    public sealed partial class AssemblySymbolData(IAssemblySymbol symbol)
    {
        public bool MightContainTagHelpers { get; } = CalculateMightContainTagHelpers(symbol);

        private static bool CalculateMightContainTagHelpers(IAssemblySymbol assembly)
        {
            // In order to contain tag helpers, components, or anything else we might want to find,
            // the assembly must start with "Microsoft.AspNetCore." or reference an assembly that
            // starts with "Microsoft.AspNetCore."
            return assembly.Name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal) ||
                    assembly.Modules.First().ReferencedAssemblies.Any(
                        a => a.Name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal));
        }
    }
}
