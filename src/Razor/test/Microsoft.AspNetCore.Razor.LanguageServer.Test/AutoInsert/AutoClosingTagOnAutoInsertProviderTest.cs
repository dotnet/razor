// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert;

public class AutoClosingTagOnAutoInsertProviderTest(ITestOutputHelper testOutput) : RazorOnAutoInsertProviderTestBase(testOutput)
{
    private static TagHelperDescriptor CatchAllTagHelper
    {
        get
        {
            var descriptor = TagHelperDescriptorBuilder.Create("CatchAllTagHelper", "TestAssembly");
            descriptor.SetMetadata(TypeName("TestNamespace.CatchAllTagHelper"));
            descriptor.TagMatchingRule(builder => builder.RequireTagName("*").RequireTagStructure(TagStructure.Unspecified));

            return descriptor.Build();
        }
    }

    private static TagHelperDescriptor UnspecifiedInputMirroringTagHelper
    {
        get
        {
            var descriptor = TagHelperDescriptorBuilder.Create("TestTagHelper", "TestAssembly");
            descriptor.SetMetadata(TypeName("TestNamespace.TestTagHelper"));
            descriptor.TagMatchingRule(builder => builder.RequireTagName("Input").RequireTagStructure(TagStructure.Unspecified));

            return descriptor.Build();
        }
    }

    private static TagHelperDescriptor UnspecifiedTagHelper
    {
        get
        {
            var descriptor = TagHelperDescriptorBuilder.Create("TestTagHelper", "TestAssembly");
            descriptor.SetMetadata(TypeName("TestNamespace.TestTagHelper"));
            descriptor.TagMatchingRule(builder => builder.RequireTagName("test").RequireTagStructure(TagStructure.Unspecified));

            return descriptor.Build();
        }
    }

    private static TagHelperDescriptor UnspecifiedInputTagHelper
    {
        get
        {
            var descriptor = TagHelperDescriptorBuilder.Create("TestInputTagHelper", "TestAssembly");
            descriptor.SetMetadata(TypeName("TestNamespace.TestInputTagHelper"));
            descriptor.TagMatchingRule(builder => builder.RequireTagName("input").RequireTagStructure(TagStructure.Unspecified));

            return descriptor.Build();
        }
    }

    private static TagHelperDescriptor NormalOrSelfclosingInputTagHelper
    {
        get
        {
            var descriptor = TagHelperDescriptorBuilder.Create("TestInputTagHelper", "TestAssembly");
            descriptor.SetMetadata(TypeName("TestNamespace.TestInputTagHelper"));
            descriptor.TagMatchingRule(builder => builder.RequireTagName("input").RequireTagStructure(TagStructure.NormalOrSelfClosing));

            return descriptor.Build();
        }
    }

    private static TagHelperDescriptor NormalOrSelfClosingTagHelper
    {
        get
        {
            var descriptor = TagHelperDescriptorBuilder.Create("TestTagHelper2", "TestAssembly");
            descriptor.SetMetadata(TypeName("TestNamespace.TestTagHelper2"));
            descriptor.TagMatchingRule(builder => builder.RequireTagName("test").RequireTagStructure(TagStructure.NormalOrSelfClosing));

            return descriptor.Build();
        }
    }

    private static TagHelperDescriptor WithoutEndTagTagHelper
    {
        get
        {
            var descriptor = TagHelperDescriptorBuilder.Create("TestTagHelper3", "TestAssembly");
            descriptor.SetMetadata(TypeName("TestNamespace.TestTagHelper3"));
            descriptor.TagMatchingRule(builder => builder.RequireTagName("test").RequireTagStructure(TagStructure.WithoutEndTag));

            return descriptor.Build();
        }
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/6217")]
    public async Task OnTypeCloseAngle_ConflictingAutoClosingBehaviorsChoosesMostSpecificAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
@addTagHelper *, TestAssembly

<test>$$
",
expected: @"
@addTagHelper *, TestAssembly

<test />
",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { WithoutEndTagTagHelper, CatchAllTagHelper });

    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36125")]
    public async Task OnTypeCloseAngle_TagHelperAlreadyHasEndTagAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
@addTagHelper *, TestAssembly

<test>$$<test></test></test>
",
expected: @"
@addTagHelper *, TestAssembly

<test><test></test></test>
",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36125")]
    public async Task OnTypeCloseAngle_VoidTagHelperHasEndTag_ShouldStillAutoCloseAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <input>$$<input></input></input>
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <input /><input></input></input>
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { UnspecifiedInputTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36125")]
    public async Task OnTypeCloseAngle_TagAlreadyHasEndTagAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    <div>$$<div></div></div>
    ",
expected: @"
    <div><div></div></div>
    ");
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36125")]
    public async Task OnTypeCloseAngle_TagDoesNotAutoCloseOutOfScopeAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    <div>
        @if (true)
        {
            <div>$$</div>
        }
    ",
expected: @"
    <div>
        @if (true)
        {
            <div></div>
        }
    ");
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36125")]
    public async Task OnTypeCloseAngle_VoidTagHasEndTag_ShouldStilloseAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    <input>$$<input></input></input>
    ",
expected: @"
    <input /><input></input></input>
    ");
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36568")]
    public async Task OnTypeCloseAngle_VoidElementMirroringTagHelperAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <Input>$$
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <Input>$0</Input>
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { UnspecifiedInputMirroringTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36568")]
    public async Task OnTypeCloseAngle_VoidHtmlElementCapitalized_SelfClosesAsync()
    {
        await RunAutoInsertTestAsync(
input: "<Input>$$",
expected: "<Input />",
fileKind: FileKinds.Legacy,
tagHelpers: Array.Empty<TagHelperDescriptor>());
    }

    [Fact]
    public async Task OnTypeCloseAngle_NormalOrSelfClosingStructureOverridesVoidTagBehaviorAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <input>$$
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <input>$0</input>
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfclosingInputTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_UnspeccifiedStructureInheritsVoidTagBehaviorAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <input>$$
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <input />
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { UnspecifiedInputTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_UnspeccifiedTagHelperTagStructureAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <test>$$
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <test>$0</test>
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { UnspecifiedTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_NormalOrSelfClosingTagHelperTagStructureAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <test>$$
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <test>$0</test>
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/33930")]
    public async Task OnTypeCloseAngle_TagHelperInHtml_NestedStatementAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test>$$</div>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test>$0</test></div>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5694")]
    public async Task OnTypeCloseAngle_HtmlTagInHtml_NestedStatement_WithAttributeAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><a target=""_blank"">$$</div>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><a target=""_blank"">$0</a></div>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5694")]
    public async Task OnTypeCloseAngle_HtmlTagInHtml_NestedStatement_WithAttribute_SpaceBetweenClosingAngleAndAttributeClosingQuoteAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><a target=""_blank"" >$$</div>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><a target=""_blank"" >$0</a></div>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5694")]
    public async Task OnTypeCloseAngle_HtmlTagInHtml_NestedStatement_WithMinimalizedAttributeAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><form novalidate>$$</div>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><form novalidate>$0</form></div>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5694")]
    public async Task OnTypeCloseAngle_HtmlTagInHtml_NestedStatement_WithMinimalizedAttribute_SpaceBetweenClosingAngleAndAttributeClosingQuoteAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><form novalidate >$$</div>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><form novalidate >$0</form></div>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5694")]
    public async Task OnTypeCloseAngle_TagHelperInHtml_NestedStatement_WithAttributeAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test attribute=""value"">$$</div>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test attribute=""value"">$0</test></div>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5694")]
    public async Task OnTypeCloseAngle_TagHelperInHtml_NestedStatement_WithAttribute_SpaceBetweenClosingAngleAndAttributeClosingQuoteAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test attribute=""value"" >$$</div>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test attribute=""value"" >$0</test></div>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5694")]
    public async Task OnTypeCloseAngle_TagHelperInHtml_NestedStatement_WithMinimalizedAttributeAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test bool-val>$$</div>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test bool-val>$0</test></div>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5694")]
    public async Task OnTypeCloseAngle_TagHelperInHtml_NestedStatement_WithMinimalizedAttribute_SpaceBetweenClosingAngleAndAttributeClosingQuoteAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test bool-val >$$</div>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test bool-val >$0</test></div>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/33930")]
    public async Task OnTypeCloseAngle_TagHelperInTagHelper_NestedStatementAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <test><input>$$</test>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <test><input /></test>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper, UnspecifiedInputTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36906")]
    public async Task OnTypeCloseAngle_TagHelperNextToVoidTagHelper_NestedStatementAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <test>$$<input />
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <test>$0</test><input />
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper, UnspecifiedInputTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36906")]
    public async Task OnTypeCloseAngle_TagHelperNextToTagHelper_NestedStatementAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <test>$$<input></input>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <test>$0</test><input></input>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper, NormalOrSelfclosingInputTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_NormalOrSelfClosingTagHelperTagStructure_CodeBlockAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @{
        <test>$$
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @{
        <test>$0</test>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_WithSlash_WithoutEndTagTagHelperTagStructureAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <test />$$
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <test />
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { WithoutEndTagTagHelper });
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/33930")]
    public async Task OnTypeCloseAngle_NestedStatementAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test />$$</div>
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @if (true)
    {
    <div><test /></div>
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { WithoutEndTagTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_WithSpace_WithoutEndTagTagHelperTagStructureAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <test >$$
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <test />
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { WithoutEndTagTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_WithoutEndTagTagHelperTagStructureAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <test>$$
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <test />
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { WithoutEndTagTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_WithoutEndTagTagHelperTagStructure_CodeBlockAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    @{
        <test>$$
    }
    ",
expected: @"
    @addTagHelper *, TestAssembly

    @{
        <test />
    }
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { WithoutEndTagTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_MultipleApplicableTagHelperTagStructuresAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <test>$$
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <test>$0</test>
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { UnspecifiedTagHelper, NormalOrSelfClosingTagHelper, WithoutEndTagTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_EscapedTagTagHelperAutoCompletesWithEscapeAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @addTagHelper *, TestAssembly

    <!test>$$
    ",
expected: @"
    @addTagHelper *, TestAssembly

    <!test>$0</!test>
    ",
fileKind: FileKinds.Legacy,
tagHelpers: new[] { NormalOrSelfClosingTagHelper });
    }

    [Fact]
    public async Task OnTypeCloseAngle_AlwaysClosesStandardHTMLTagAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
        <div><div>$$</div>
    ",
expected: @"
        <div><div>$0</div></div>
    ");
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/33930")]
    public async Task OnTypeCloseAngle_ClosesStandardHTMLTag_NestedStatementAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @if (true)
    {
        <div><p>$$</div>
    }
    ",
expected: @"
    @if (true)
    {
        <div><p>$0</p></div>
    }
    ");
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36906")]
    public async Task OnTypeCloseAngle_TagNextToTag_NestedStatementAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @if (true)
    {
        <p>$$<div></div>
    }
    ",
expected: @"
    @if (true)
    {
        <p>$0</p><div></div>
    }
    ");
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/36906")]
    public async Task OnTypeCloseAngle_TagNextToVoidTag_NestedStatementAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @if (true)
    {
        <p>$$<input />
    }
    ",
expected: @"
    @if (true)
    {
        <p>$0</p><input />
    }
    ");
    }

    [Fact]
    public async Task OnTypeCloseAngle_ClosesStandardHTMLTagAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
        <div>$$
    ",
expected: @"
        <div>$0</div>
    ");
    }

    [Fact]
    public async Task OnTypeCloseAngle_ClosesStandardHTMLTag_CodeBlockAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @{
        <div>$$
    }
    ",
expected: @"
    @{
        <div>$0</div>
    }
    ");
    }

    [Fact]
    public async Task OnTypeCloseAngle_ClosesVoidHTMLTagAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
        <input>$$
    ",
expected: @"
        <input />
    ");
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/33930")]
    public async Task OnTypeCloseAngle_ClosesVoidHTMLTag_NestedStatementAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @if (true)
    {
        <strong><input>$$</strong>
    }
    ",
expected: @"
    @if (true)
    {
        <strong><input /></strong>
    }
    ");
    }

    [Fact]
    public async Task OnTypeCloseAngle_ClosesVoidHTMLTag_CodeBlockAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    @{
        <input>$$
    }
    ",
expected: @"
    @{
        <input />
    }
    ");
    }

    [Fact]
    public async Task OnTypeCloseAngle_WithSlash_ClosesVoidHTMLTagAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
        <input />$$
    ",
expected: @"
        <input />
    ");
    }

    [Fact]
    public async Task OnTypeCloseAngle_WithSpace_ClosesVoidHTMLTagAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
        <input >$$
    ",
expected: @"
        <input />
    ");
    }

    [Fact]
    public async Task OnTypeCloseAngle_AutoInsertDisabled_NoopsAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
        <div>$$
    ",
expected: @"
        <div>
    ",
            enableAutoClosingTags: false);
    }

    internal override IOnAutoInsertProvider CreateProvider()
        => new AutoClosingTagOnAutoInsertProvider();
}
