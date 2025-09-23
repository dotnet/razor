﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class TagHelperIntermediateNode : IntermediateNode
{
    public required TagMode TagMode { get; init; }
    public required string TagName { get; init; }

    public ImmutableArray<TagHelperDescriptor> TagHelpers { get; init => field = value.NullToEmpty(); } = [];

    public override IntermediateNodeCollection Children { get => field ??= []; }

    public override void Accept(IntermediateNodeVisitor visitor)
        => visitor.VisitTagHelper(this);

    public override void FormatNode(IntermediateNodeFormatter formatter)
    {
        formatter.WriteContent(TagName);

        formatter.WriteProperty(nameof(TagHelpers), string.Join(", ", TagHelpers.Select(t => t.DisplayName)));
        formatter.WriteProperty(nameof(TagMode), TagMode.ToString());
        formatter.WriteProperty(nameof(TagName), TagName);
    }
}
