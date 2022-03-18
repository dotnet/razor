// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.Editor.Razor;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert
{
    public class AttributeSnippetOnAutoInsertProviderTest : RazorOnAutoInsertProviderTestBase
    {
        [Fact]
        public void OnTypeEqual_AfterTagHelperIntAttribute_TriggersAttributeValueSnippet()
        {
            RunAutoInsertTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <span intAttribute=$$></span>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <span intAttribute=""$0""></span>
</section>
",
fileKind: FileKinds.Legacy,
tagHelpers: TagHelpers);
        }

        [Fact]
        [WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1452432")]
        public void OnTypeEqual_AfterTagHelperDictionaryIntAttribute_Noops()
        {
            RunAutoInsertTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <form asp-route-foo=$$></form>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <form asp-route-foo=></form>
</section>
",
fileKind: FileKinds.Legacy,
tagHelpers: TagHelpers);
        }

        [Fact]
        public void OnTypeEqual_AfterNonTagHelperAttribute_Noops()
        {
            RunAutoInsertTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <span test2=$$></span>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <span test2=></span>
</section>
",
fileKind: FileKinds.Legacy,
tagHelpers: TagHelpers);
        }

        [Fact]
        public void OnTypeEqual_AfterTagHelperStringAttribute_Noops()
        {
            RunAutoInsertTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <span stringAttribute=$$></span>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <span stringAttribute=></span>
</section>
",
fileKind: FileKinds.Legacy,
tagHelpers: TagHelpers);
        }

        [Fact]
        public void OnTypeEqual_AfterTagHelperTag_Noops()
        {
            RunAutoInsertTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <span=$$></span>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <span=></span>
</section>
",
fileKind: FileKinds.Legacy,
tagHelpers: TagHelpers);
        }

        [Fact]
        public void OnTypeEqual_AfterTagHelperAttributeEqual_Noops()
        {
            RunAutoInsertTest(
input: @"
@addTagHelper *, TestAssembly

<section>
    <span intAttribute==$$></span>
</section>
",
expected: $@"
@addTagHelper *, TestAssembly

<section>
    <span intAttribute==></span>
</section>
",
fileKind: FileKinds.Legacy,
tagHelpers: TagHelpers);
        }

        internal override RazorOnAutoInsertProvider CreateProvider()
        {
            var provider = new AttributeSnippetOnAutoInsertProvider(new DefaultTagHelperFactsService(), LoggerFactory);
            return provider;
        }

        static TagHelperDescriptor[] TagHelpers
        {
            get
            {
                var basicDescriptor = TagHelperDescriptorBuilder.Create("SpanTagHelper", "TestAssembly");
                basicDescriptor.SetTypeName("TestNamespace.SpanTagHelper");
                basicDescriptor.TagMatchingRule(builder => builder.RequireTagName("span"));
                basicDescriptor.BindAttribute(builder =>
                    builder
                        .Name("intAttribute")
                        .PropertyName("intAttribute")
                        .TypeName(typeof(int).FullName));
                basicDescriptor.BindAttribute(builder =>
                    builder
                        .Name("stringAttribute")
                        .PropertyName("stringAttribute")
                        .TypeName(typeof(string).FullName));

                var prefixDescriptor = TagHelperDescriptorBuilder.Create("FormTagHelper", "TestAssembly");
                prefixDescriptor.SetTypeName("TestNamespace.FormTagHelper");
                prefixDescriptor.TagMatchingRuleDescriptor(rule => rule.RequireTagName("form"));
                prefixDescriptor.BoundAttributeDescriptor(builder =>
                    builder
                        .TypeName("System.Collections.Generic.IDictionary<System.String, System.Boolean>")
                        .PropertyName("RouteValues").AsDictionary("asp-route-", typeof(bool).FullName));

                return new[]
                {
                    basicDescriptor.Build(),
                    prefixDescriptor.Build(),
                };
            }
        }
    }
}
