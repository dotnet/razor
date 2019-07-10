// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language
{
    internal class TagHelperDescriptorComparer : IEqualityComparer<TagHelperDescriptor>
    {
        /// <summary>
        /// A default instance of the <see cref="TagHelperDescriptorComparer"/>.
        /// </summary>
        public static readonly TagHelperDescriptorComparer Default = new TagHelperDescriptorComparer();

        private TagHelperDescriptorComparer()
        {
        }

        public virtual bool Equals(TagHelperDescriptor descriptorX, TagHelperDescriptor descriptorY)
        {
            if (object.ReferenceEquals(descriptorX, descriptorY))
            {
                return true;
            }

            if (descriptorX == null ^ descriptorY == null)
            {
                return false;
            }

            if (descriptorX.CaseSensitive != descriptorY.CaseSensitive)
            {
                return false;
            }

            var stringComparer = descriptorX.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            var stringComparison = descriptorX.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (!string.Equals(descriptorX.Kind, descriptorY.Kind, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(descriptorX.AssemblyName, descriptorY.AssemblyName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(descriptorX.Name, descriptorY.Name, StringComparison.Ordinal))
            {
                return false;
            }

            if (!Enumerable.SequenceEqual(
                descriptorX.BoundAttributes.OrderBy(attribute => attribute.Name, stringComparer),
                descriptorY.BoundAttributes.OrderBy(attribute => attribute.Name, stringComparer),
                BoundAttributeDescriptorComparer.Default))
            {
                return false;
            }

            if (!Enumerable.SequenceEqual(
                descriptorX.TagMatchingRules.OrderBy(rule => rule.TagName, stringComparer),
                descriptorY.TagMatchingRules.OrderBy(rule => rule.TagName, stringComparer),
                TagMatchingRuleDescriptorComparer.Default))
            {
                return false;
            }

            if (!(descriptorX.AllowedChildTags == descriptorY.AllowedChildTags ||
                (descriptorX.AllowedChildTags != null &&
                descriptorY.AllowedChildTags != null &&
                Enumerable.SequenceEqual(
                    descriptorX.AllowedChildTags.OrderBy(childTag => childTag.Name, stringComparer),
                    descriptorY.AllowedChildTags.OrderBy(childTag => childTag.Name, stringComparer),
                    AllowedChildTagDescriptorComparer.Default))))
            {
                return false;
            }

            if (!string.Equals(descriptorX.Documentation, descriptorY.Documentation, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(descriptorX.DisplayName, descriptorY.DisplayName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(descriptorX.TagOutputHint, descriptorY.TagOutputHint, stringComparison))
            {
                return false;
            }

            if (!Enumerable.SequenceEqual(descriptorX.Diagnostics, descriptorY.Diagnostics))
            {
                return false;
            }

            if (!Enumerable.SequenceEqual(
                descriptorX.Metadata.OrderBy(metadataX => metadataX.Key, StringComparer.Ordinal),
                descriptorY.Metadata.OrderBy(metadataY => metadataY.Key, StringComparer.Ordinal)))
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public virtual int GetHashCode(TagHelperDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var hash = HashCodeCombiner.Start();
            hash.Add(descriptor.Kind, StringComparer.Ordinal);
            hash.Add(descriptor.AssemblyName, StringComparer.Ordinal);
            hash.Add(descriptor.Name, StringComparer.Ordinal);

            return hash.CombinedHash;
        }
    }
}