﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.IO;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace Microsoft.AspNetCore.Razor.TagHelpers;

public class TagHelperAttribute : IHtmlContentContainer
{
    public TagHelperAttribute(string name)
    {
    }

    public TagHelperAttribute(string name, object value)
    {
    }

    public TagHelperAttribute(string name, object value, HtmlAttributeValueStyle valueStyle)
    {
    }

    public string Name { get; }

    public object Value { get; }

    public HtmlAttributeValueStyle ValueStyle { get; }

    public void WriteTo(TextWriter writer, HtmlEncoder encoder)
    {
    }

    public void CopyTo(IHtmlContentBuilder destination)
    {
    }

    public void MoveTo(IHtmlContentBuilder destination)
    {
    }
}