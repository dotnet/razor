// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    internal record TagHelperDeltaResult(
        bool Delta,
        int ResultId,
        IReadOnlyList<TagHelperDescriptor> Added,
        IReadOnlyList<TagHelperDescriptor> Removed)
    {
        public IReadOnlyList<TagHelperDescriptor> Apply(IReadOnlyList<TagHelperDescriptor> baseTagHelpers)
        {
            if (Added.Count == 0 && Removed.Count == 0)
            {
                return baseTagHelpers;
            }

            var newTagHelpers = new List<TagHelperDescriptor>(baseTagHelpers.Count + Added.Count - Removed.Count);
            newTagHelpers.AddRange(Added);

            foreach (var existingTagHelper in baseTagHelpers)
            {
                if (!Removed.Contains(existingTagHelper))
                {
                    newTagHelpers.Add(existingTagHelper);
                }
            }

            return newTagHelpers;
        }
    }
}
