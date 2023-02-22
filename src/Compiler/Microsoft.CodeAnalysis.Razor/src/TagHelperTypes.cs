// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor;

internal static class TagHelperTypes
{
    public const string ITagHelper = WellKnownTypeNames.MicrosoftAspNetCoreRazorTagHelpersITagHelper;

    public const string IComponent = WellKnownTypeNames.MicrosoftAspNetCoreComponentsIComponent;

    public const string IDictionary = WellKnownTypeNames.SystemCollectionsGenericIDictionary2;

    public const string HtmlAttributeNameAttribute = WellKnownTypeNames.MicrosoftAspNetCoreRazorTagHelpersHtmlAttributeNameAttribute;

    public const string HtmlAttributeNotBoundAttribute = WellKnownTypeNames.MicrosoftAspNetCoreRazorTagHelpersHtmlAttributeNotBoundAttribute;

    public const string HtmlTargetElementAttribute = WellKnownTypeNames.MicrosoftAspNetCoreRazorTagHelpersHtmlTargetElementAttribute;

    public const string OutputElementHintAttribute = WellKnownTypeNames.MicrosoftAspNetCoreRazorTagHelpersOutputElementHintAttribute;

    public const string RestrictChildrenAttribute = WellKnownTypeNames.MicrosoftAspNetCoreRazorTagHelpersRestrictChildrenAttribute;

    public static class HtmlAttributeName
    {
        public const string Name = "Name";
        public const string DictionaryAttributePrefix = "DictionaryAttributePrefix";
    }

    public static class HtmlTargetElement
    {
        public const string Attributes = "Attributes";

        public const string ParentTag = "ParentTag";

        public const string TagStructure = "TagStructure";
    }
}
