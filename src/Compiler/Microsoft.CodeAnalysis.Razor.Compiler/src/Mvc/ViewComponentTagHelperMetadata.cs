﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public static class ViewComponentTagHelperMetadata
{
    /// <summary>
    /// The key in a <see cref="Microsoft.AspNetCore.Razor.Language.TagHelperDescriptor.Metadata"/>  containing
    /// the short name of a view component.
    /// </summary>
    public static readonly string Name = "MVC.ViewComponent.Name";
}
