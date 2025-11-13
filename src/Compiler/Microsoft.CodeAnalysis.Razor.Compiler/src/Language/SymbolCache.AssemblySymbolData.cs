// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class SymbolCache
{
    public sealed partial class AssemblySymbolData(IAssemblySymbol symbol)
    {
        private const int IncludeDocumentation = 1 << 0;
        private const int ExcludeHidden = 1 << 1;

        // The cache needs to be large enough to handle all combinations of options.
        private const int CacheSize = (IncludeDocumentation | ExcludeHidden) + 1;

        private readonly TagHelperCollection[] _tagHelpers = new TagHelperCollection[CacheSize];

        public bool TryGetTagHelpers(bool includeDocumentation, bool excludeHidden, [NotNullWhen(true)] out TagHelperCollection? tagHelpers)
        {
            var index = CalculateIndex(includeDocumentation, excludeHidden);

            tagHelpers = Volatile.Read(ref _tagHelpers[index]);
            return tagHelpers is not null;
        }

        public TagHelperCollection AddTagHelpers(TagHelperCollection tagHelpers, bool includeDocumentation, bool excludeHidden)
        {
            var index = CalculateIndex(includeDocumentation, excludeHidden);

            return InterlockedOperations.Initialize(ref _tagHelpers[index], tagHelpers);
        }

        private static int CalculateIndex(bool includeDocumentation, bool excludeHidden)
        {
            var index = 0;

            if (includeDocumentation)
            {
                index |= IncludeDocumentation;
            }

            if (excludeHidden)
            {
                index |= ExcludeHidden;
            }

            return index;
        }

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
