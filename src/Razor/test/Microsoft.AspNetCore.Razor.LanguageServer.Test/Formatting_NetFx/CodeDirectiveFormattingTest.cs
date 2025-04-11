﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

[Collection(HtmlFormattingCollection.Name)]
public class CodeDirectiveFormattingTest(FormattingTestContext context, HtmlFormattingFixture fixture, ITestOutputHelper testOutput)
    : FormattingTestBase(context, fixture.Service, testOutput), IClassFixture<FormattingTestContext>
{
    [FormattingTestFact(SkipFlipLineEnding = true)]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5648")]
    public async Task GenericComponentWithCascadingTypeParameter()
    {
        await RunFormattingTestAsync(
            input: """
                    @page "/counter"

                    @if(true)
                        {
                                    // indented
                            }

                    <TestGeneric Items="_items">
                        @foreach (var v in System.Linq.Enumerable.Range(1, 10))
                            {
                                <div></div>
                            }
                        </TestGeneric>

                    @if(true)
                        {
                                    // indented
                                }

                    @code
                        {
                        private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
                    }
                    """,
            expected: """
                    @page "/counter"

                    @if (true)
                    {
                        // indented
                    }

                    <TestGeneric Items="_items">
                        @foreach (var v in System.Linq.Enumerable.Range(1, 10))
                        {
                            <div></div>
                        }
                    </TestGeneric>

                    @if (true)
                    {
                        // indented
                    }

                    @code
                    {
                        private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
                    }
                    """,
            tagHelpers: GetComponentWithCascadingTypeParameter());
    }

    [FormattingTestFact]
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5648")]
    public async Task GenericComponentWithCascadingTypeParameter_Nested()
    {
        await RunFormattingTestAsync(
            input: """
                    @page "/counter"

                    <TestGeneric Items="_items">
                        @foreach (var v in System.Linq.Enumerable.Range(1, 10))
                            {
                                <div></div>
                            }
                    <TestGeneric Items="_items">
                        @foreach (var v in System.Linq.Enumerable.Range(1, 10))
                            {
                                <div></div>
                            }
                        </TestGeneric>
                        </TestGeneric>

                    @code
                        {
                        private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
                    }
                    """,
            expected: """
                    @page "/counter"

                    <TestGeneric Items="_items">
                        @foreach (var v in System.Linq.Enumerable.Range(1, 10))
                        {
                            <div></div>
                        }
                        <TestGeneric Items="_items">
                            @foreach (var v in System.Linq.Enumerable.Range(1, 10))
                            {
                                <div></div>
                            }
                        </TestGeneric>
                    </TestGeneric>

                    @code
                    {
                        private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
                    }
                    """,
            tagHelpers: GetComponentWithCascadingTypeParameter());
    }

    [FormattingTestFact(SkipFlipLineEnding = true)] // tracked by https://github.com/dotnet/razor/issues/10836
    [WorkItem("https://github.com/dotnet/razor-tooling/issues/5648")]
    public async Task GenericComponentWithCascadingTypeParameter_MultipleParameters()
    {
        await RunFormattingTestAsync(
            input: """
                    @page "/counter"

                    <TestGenericTwo Items="_items" ItemsTwo="_items2">
                        @foreach (var v in System.Linq.Enumerable.Range(1, 10))
                            {
                                <div></div>
                            }
                        </TestGenericTwo>

                    @code
                        {
                        private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
                        private IEnumerable<long> _items2 = new long[] { 1, 2, 3, 4, 5 };
                    }
                    """,
            expected: """
                    @page "/counter"

                    <TestGenericTwo Items="_items" ItemsTwo="_items2">
                        @foreach (var v in System.Linq.Enumerable.Range(1, 10))
                        {
                            <div></div>
                        }
                    </TestGenericTwo>

                    @code
                    {
                        private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
                        private IEnumerable<long> _items2 = new long[] { 1, 2, 3, 4, 5 };
                    }
                    """,
            tagHelpers: GetComponentWithTwoCascadingTypeParameter());
    }

    private ImmutableArray<TagHelperDescriptor> GetComponentWithCascadingTypeParameter()
    {
        var input = """
                @using System.Collections.Generic
                @using Microsoft.AspNetCore.Components
                @typeparam TItem
                @attribute [CascadingTypeParameter(nameof(TItem))]

                <h3>TestGeneric</h3>

                @code
                {
                    [Parameter] public IEnumerable<TItem> Items { get; set; }
                    [Parameter] public RenderFragment ChildContent { get; set; }
                }
                """;

        var generated = CompileToCSharp("TestGeneric.razor", input, throwOnFailure: true, fileKind: FileKinds.Component);

        return generated.CodeDocument.GetTagHelperContext().TagHelpers.ToImmutableArray();
    }

    private ImmutableArray<TagHelperDescriptor> GetComponentWithTwoCascadingTypeParameter()
    {
        var input = """
                @using System.Collections.Generic
                @using Microsoft.AspNetCore.Components
                @typeparam TItem
                @typeparam TItemTwo
                @attribute [CascadingTypeParameter(nameof(TItem))]
                @attribute [CascadingTypeParameter(nameof(TItemTwo))]

                <h3>TestGeneric</h3>

                @code
                {
                    [Parameter] public IEnumerable<TItem> Items { get; set; }
                    [Parameter] public IEnumerable<TItemTwo> ItemsTwo { get; set; }
                    [Parameter] public RenderFragment ChildContent { get; set; }
                }
                """;

        var generated = CompileToCSharp("TestGenericTwo.razor", input, throwOnFailure: true, fileKind: FileKinds.Component);

        return generated.CodeDocument.GetTagHelperContext().TagHelpers.ToImmutableArray();
    }
}
