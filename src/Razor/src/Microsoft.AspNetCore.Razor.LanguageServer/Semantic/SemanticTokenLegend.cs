// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    public class SemanticTokenLegend
    {
        public const string RazorTagHelperElement = "razorTagHelperElement";
        public const string RazorTagHelperAttribute = "razorTagHelperAttribute";
        private static readonly string[] _tokenTypes = new string[] {
            RazorTagHelperElement,
            RazorTagHelperAttribute,
        };

        private static readonly string[] _tokenModifiers = new string[] {
            "static", "async"
        };

        public IDictionary<string, int> TokenTypesLegend
        {
            get
            {
                return GetMap(_tokenTypes);
            }
        }

        public IDictionary<string, int> TokenModifiersLegend
        {
            get
            {
                return GetMap(_tokenModifiers);
            }
        }

        private static IDictionary<string, int> GetMap(IEnumerable<string> tokens)
        {
            var result = new Dictionary<string, int>();
            for (var i = 0; i < tokens.Count(); i++)
            {
                result.Add(tokens.ElementAt(i), i);
            }

            return result;
        }
    }
}
