// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public class AttributeSnippetFormatOnTypeProviderTest : FormatOnTypeProviderTestBase
    {
        [Fact]
        public void OnTypeEqual_AfterTagHelperIntAttribute_TriggersAttributeValueSnippet()
        {
            RunFormatOnTypeTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <span test|></span>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <span test=""{ LanguageServerConstants.CursorPlaceholderString}""></span>
</section>
",
character: "=",
fileKind: FileKinds.Legacy,
tagHelpers: GetTagHelpers(typeof(int).FullName));
        }

        [Fact]
        public void OnTypeEqual_AfterNonTagHelperAttribute_Noops()
        {
            RunFormatOnTypeTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <span test2|></span>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <span test2=></span>
</section>
",
character: "=",
fileKind: FileKinds.Legacy,
tagHelpers: GetTagHelpers(typeof(int).FullName));
        }

        [Fact]
        public void OnTypeEqual_AfterTagHelperStringAttribute_Noops()
        {
            RunFormatOnTypeTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <span test|></span>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <span test=></span>
</section>
",
character: "=",
fileKind: FileKinds.Legacy,
tagHelpers: GetTagHelpers(typeof(string).FullName));
        }

        [Fact]
        public void OnTypeEqual_AfterTagHelperTag_Noops()
        {
            RunFormatOnTypeTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <span|></span>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <span=></span>
</section>
",
character: "=",
fileKind: FileKinds.Legacy,
tagHelpers: GetTagHelpers(typeof(int).FullName));
        }

        [Fact]
        public void OnTypeEqual_AfterTagHelperAttributeEqual_Noops()
        {
            RunFormatOnTypeTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <span test=|></span>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <span test==></span>
</section>
",
character: "=",
fileKind: FileKinds.Legacy,
tagHelpers: GetTagHelpers(typeof(int).FullName));
        }

        internal override RazorFormatOnTypeProvider CreateProvider()
        {
            var provider = new AttributeSnippetFormatOnTypeProvider();
            return provider;
        }

        internal IReadOnlyList<TagHelperDescriptor> GetTagHelpers(string attributeType)
        {
            var descriptor = TagHelperDescriptorBuilder.Create("SpanTagHelper", "TestAssembly");
            descriptor.SetTypeName("TestNamespace.SpanTagHelper");
            descriptor.TagMatchingRule(builder => builder.RequireTagName("span"));
            descriptor.BindAttribute(builder =>
                builder
                    .Name("test")
                    .PropertyName("test")
                    .TypeName(attributeType));

            return new[]
            {
                    descriptor.Build()
            };
        }
    }
}
