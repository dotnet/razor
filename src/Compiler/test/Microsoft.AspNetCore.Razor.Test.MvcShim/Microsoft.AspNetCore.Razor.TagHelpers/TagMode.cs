﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.Razor.TagHelpers;

/// <summary>
/// The mode in which an element should render.
/// </summary>
public enum TagMode
{
    /// <summary>
    /// Include both start and end tags.
    /// </summary>
    StartTagAndEndTag,

    /// <summary>
    /// A self-closed tag.
    /// </summary>
    SelfClosing,

    /// <summary>
    /// Only a start tag.
    /// </summary>
    StartTagOnly
}
