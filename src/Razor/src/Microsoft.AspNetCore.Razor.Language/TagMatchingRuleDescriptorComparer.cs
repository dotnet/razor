// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language
{
    internal class TagMatchingRuleDescriptorComparer : IEqualityComparer<TagMatchingRuleDescriptor>
    {
        /// <summary>
        /// A default instance of the <see cref="TagMatchingRuleDescriptorComparer"/>.
        /// </summary>
        public static readonly TagMatchingRuleDescriptorComparer Default = new TagMatchingRuleDescriptorComparer();

        private TagMatchingRuleDescriptorComparer()
        {
        }

        public virtual bool Equals(TagMatchingRuleDescriptor ruleX, TagMatchingRuleDescriptor ruleY)
        {
            if (object.ReferenceEquals(ruleX, ruleY))
            {
                return true;
            }

            if (ruleX == null ^ ruleY == null)
            {
                return false;
            }

            if (ruleX.CaseSensitive != ruleX.CaseSensitive)
            {
                return false;
            }

            var stringComparison = ruleX.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            return
                string.Equals(ruleX.TagName, ruleY.TagName, stringComparison) &&
                string.Equals(ruleX.ParentTag, ruleY.ParentTag, stringComparison) &&
                ruleX.TagStructure == ruleY.TagStructure &&
                Enumerable.SequenceEqual(ruleX.Attributes, ruleY.Attributes, RequiredAttributeDescriptorComparer.Default);
        }

        public virtual int GetHashCode(TagMatchingRuleDescriptor rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            var hash = HashCodeCombiner.Start();
            hash.Add(rule.TagName, rule.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

            return hash.CombinedHash;
        }
    }
}