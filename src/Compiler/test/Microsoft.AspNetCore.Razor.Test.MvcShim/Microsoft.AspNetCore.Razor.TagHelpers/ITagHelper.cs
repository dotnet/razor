﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.Razor.TagHelpers;

/// <summary>
/// Contract used to filter matching HTML elements.
/// Marker interface for <see cref="TagHelper"/>s.
/// </summary>
public interface ITagHelper : ITagHelperComponent
{
}
