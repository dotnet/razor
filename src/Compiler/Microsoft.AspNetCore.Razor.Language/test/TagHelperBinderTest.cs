﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.Language;

public class TagHelperBinderTest
{
    [Fact]
    public void GetBinding_ReturnsBindingWithInformation()
    {
        // Arrange
        var divTagHelper = TagHelperDescriptorBuilder.Create("DivTagHelper", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .Build();
        var expectedDescriptors = new[] { divTagHelper };
        var expectedAttributes = ImmutableArray.Create(
            new KeyValuePair<string, string>("class", "something"));
        var tagHelperBinder = new TagHelperBinder("th:", expectedDescriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "th:div",
            attributes: expectedAttributes,
            parentTagName: "body",
            parentIsTagHelper: false);

        // Assert
        Assert.Equal(expectedDescriptors, bindingResult.Descriptors, TagHelperDescriptorComparer.Default);
        Assert.Equal("th:div", bindingResult.TagName);
        Assert.Equal("body", bindingResult.ParentTagName);
        Assert.Equal(expectedAttributes, bindingResult.Attributes);
        Assert.Equal("th:", bindingResult.TagHelperPrefix);
        Assert.Equal(divTagHelper.TagMatchingRules, bindingResult.Mappings[divTagHelper], TagMatchingRuleDescriptorComparer.Default);
    }

    [Fact]
    public void GetBinding_With_Multiple_TagNameRules_SingleHelper()
    {
        // Arrange
        var multiTagHelper = TagHelperDescriptorBuilder.Create("MultiTagHelper", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("a"))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("img"))
            .Build();
        var expectedDescriptors = new[] { multiTagHelper };
        var tagHelperBinder = new TagHelperBinder("", expectedDescriptors);

        TestTagName("div", multiTagHelper.TagMatchingRules[0]);
        TestTagName("a", multiTagHelper.TagMatchingRules[1]);
        TestTagName("img", multiTagHelper.TagMatchingRules[2]);
        TestTagName("p", null);
        TestTagName("*", null);
        void TestTagName(string tagName, TagMatchingRuleDescriptor expectedBindingResult)
        {
            // Act
            var bindingResult = tagHelperBinder.GetBinding(

                tagName: tagName,
                attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
                parentTagName: "body",
                parentIsTagHelper: false);

            // Assert
            if (expectedBindingResult == null)
            {
                Assert.Null(bindingResult);
                return;
            }
            else
            {
                Assert.NotNull(bindingResult);
                Assert.Equal(expectedDescriptors, bindingResult.Descriptors, TagHelperDescriptorComparer.Default);

                Assert.Equal(tagName, bindingResult.TagName);
                var mapping = Assert.Single(bindingResult.Mappings[multiTagHelper]);
                Assert.Equal(expectedBindingResult, mapping, TagMatchingRuleDescriptorComparer.Default);
            }
        }
    }

    [Fact]
    public void GetBinding_With_Multiple_TagNameRules_MultipleHelpers()
    {
        // Arrange
        var multiTagHelper1 = TagHelperDescriptorBuilder.Create("MultiTagHelper1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("a"))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("img"))
            .Build();

        var multiTagHelper2 = TagHelperDescriptorBuilder.Create("MultiTagHelper2", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("p"))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("table"))
            .Build();

        var tagHelperBinder = new TagHelperBinder("", new[] { multiTagHelper1, multiTagHelper2 });

        TestTagName("div", new[] { multiTagHelper1, multiTagHelper2 }, new[] { multiTagHelper1.TagMatchingRules[0], multiTagHelper2.TagMatchingRules[0] });
        TestTagName("a", new[] { multiTagHelper1 }, new[] { multiTagHelper1.TagMatchingRules[1] });
        TestTagName("img", new[] { multiTagHelper1 }, new[] { multiTagHelper1.TagMatchingRules[2] });
        TestTagName("p", new[] { multiTagHelper2 }, new[] { multiTagHelper2.TagMatchingRules[1] });
        TestTagName("table", new[] { multiTagHelper2 }, new[] { multiTagHelper2.TagMatchingRules[2] });
        TestTagName("*", null, null);


        void TestTagName(string tagName, TagHelperDescriptor[] expectedDescriptors, TagMatchingRuleDescriptor[] expectedBindingResults)
        {
            // Act
            var bindingResult = tagHelperBinder.GetBinding(
                tagName: tagName,
                attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
                parentTagName: "body",
                parentIsTagHelper: false);

            // Assert
            if (expectedDescriptors is null)
            {
                Assert.Null(bindingResult);
            }
            else
            {
                Assert.NotNull(bindingResult);
                Assert.Equal(expectedDescriptors, bindingResult.Descriptors, TagHelperDescriptorComparer.Default);

                Assert.Equal(tagName, bindingResult.TagName);

                for (int i = 0; i < expectedDescriptors.Length; i++)
                {
                    var mapping = Assert.Single(bindingResult.Mappings[expectedDescriptors[i]]);
                    Assert.Equal(expectedBindingResults[i], mapping, TagMatchingRuleDescriptorComparer.Default);
                }
            }
        }
    }

    public static TheoryData RequiredParentData
    {
        get
        {
            var strongPDivParent = TagHelperDescriptorBuilder.Create("StrongTagHelper", "SomeAssembly")
                .TagMatchingRuleDescriptor(rule =>
                    rule
                    .RequireTagName("strong")
                    .RequireParentTag("p"))
                .TagMatchingRuleDescriptor(rule =>
                    rule
                    .RequireTagName("strong")
                    .RequireParentTag("div"))
                .Build();
            var catchAllPParent = TagHelperDescriptorBuilder.Create("CatchAllTagHelper", "SomeAssembly")
                .TagMatchingRuleDescriptor(rule =>
                    rule
                    .RequireTagName("*")
                    .RequireParentTag("p"))
                .Build();

            return new TheoryData<
                string, // tagName
                string, // parentTagName
                IReadOnlyList<TagHelperDescriptor>, // availableDescriptors
                IReadOnlyList<TagHelperDescriptor>> // expectedDescriptors
                {
                    {
                        "strong",
                        "p",
                        new[] { strongPDivParent },
                        new[] { strongPDivParent }
                    },
                    {
                        "strong",
                        "div",
                        new[] { strongPDivParent, catchAllPParent },
                        new[] { strongPDivParent }
                    },
                    {
                        "strong",
                        "p",
                        new[] { strongPDivParent, catchAllPParent },
                        new[] { strongPDivParent, catchAllPParent }
                    },
                    {
                        "custom",
                        "p",
                        new[] { strongPDivParent, catchAllPParent },
                        new[] { catchAllPParent }
                    },
                };
        }
    }

    [Theory]
    [MemberData(nameof(RequiredParentData))]
    public void GetBinding_ReturnsBindingResultWithDescriptorsParentTags(
        string tagName,
        string parentTagName,
        object availableDescriptors,
        object expectedDescriptors)
    {
        // Arrange
        var tagHelperBinder = new TagHelperBinder(null, (IReadOnlyList<TagHelperDescriptor>)availableDescriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName,
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: parentTagName,
            parentIsTagHelper: false);

        // Assert
        Assert.Equal((IEnumerable<TagHelperDescriptor>)expectedDescriptors, bindingResult.Descriptors, TagHelperDescriptorComparer.Default);
    }

    public static TheoryData RequiredAttributeData
    {
        get
        {
            var divDescriptor = TagHelperDescriptorBuilder.Create("DivTagHelper", "SomeAssembly")
                .TagMatchingRuleDescriptor(rule =>
                    rule
                    .RequireTagName("div")
                    .RequireAttributeDescriptor(attribute => attribute.Name("style")))
                .Build();
            var inputDescriptor = TagHelperDescriptorBuilder.Create("InputTagHelper", "SomeAssembly")
                .TagMatchingRuleDescriptor(rule =>
                    rule
                    .RequireTagName("input")
                    .RequireAttributeDescriptor(attribute => attribute.Name("class"))
                    .RequireAttributeDescriptor(attribute => attribute.Name("style")))
                .Build();
            var inputWildcardPrefixDescriptor = TagHelperDescriptorBuilder.Create("InputWildCardAttribute", "SomeAssembly")
                .TagMatchingRuleDescriptor(rule =>
                    rule
                    .RequireTagName("input")
                    .RequireAttributeDescriptor(attribute =>
                        attribute
                        .Name("nodashprefix")
                        .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.PrefixMatch)))
                .Build();
            var catchAllDescriptor = TagHelperDescriptorBuilder.Create("CatchAllTagHelper", "SomeAssembly")
                .TagMatchingRuleDescriptor(rule =>
                    rule
                    .RequireTagName(TagHelperMatchingConventions.ElementCatchAllName)
                    .RequireAttributeDescriptor(attribute => attribute.Name("class")))
                .Build();
            var catchAllDescriptor2 = TagHelperDescriptorBuilder.Create("CatchAllTagHelper2", "SomeAssembly")
                .TagMatchingRuleDescriptor(rule =>
                    rule
                    .RequireTagName(TagHelperMatchingConventions.ElementCatchAllName)
                    .RequireAttributeDescriptor(attribute => attribute.Name("custom"))
                    .RequireAttributeDescriptor(attribute => attribute.Name("class")))
                .Build();
            var catchAllWildcardPrefixDescriptor = TagHelperDescriptorBuilder.Create("CatchAllWildCardAttribute", "SomeAssembly")
                .TagMatchingRuleDescriptor(rule =>
                    rule
                    .RequireTagName(TagHelperMatchingConventions.ElementCatchAllName)
                    .RequireAttributeDescriptor(attribute =>
                        attribute
                        .Name("prefix-")
                        .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.PrefixMatch)))
                .Build();
            var defaultAvailableDescriptors =
                new[] { divDescriptor, inputDescriptor, catchAllDescriptor, catchAllDescriptor2 };
            var defaultWildcardDescriptors =
                new[] { inputWildcardPrefixDescriptor, catchAllWildcardPrefixDescriptor };
            Func<string, KeyValuePair<string, string>> kvp =
                (name) => new KeyValuePair<string, string>(name, "test value");

            return new TheoryData<
                string, // tagName
                ImmutableArray<KeyValuePair<string, string>>, // providedAttributes
                IEnumerable<TagHelperDescriptor>, // availableDescriptors
                IEnumerable<TagHelperDescriptor>> // expectedDescriptors
                {
                    {
                        "div",
                        ImmutableArray.Create(kvp("custom")),
                        defaultAvailableDescriptors,
                        null
                    },
                    { "div", ImmutableArray.Create(kvp("style")), defaultAvailableDescriptors, new[] { divDescriptor } },
                    { "div", ImmutableArray.Create(kvp("class")), defaultAvailableDescriptors, new[] { catchAllDescriptor } },
                    {
                        "div",
                        ImmutableArray.Create(kvp("class"), kvp("style")),
                        defaultAvailableDescriptors,
                        new[] { divDescriptor, catchAllDescriptor }
                    },
                    {
                        "div",
                        ImmutableArray.Create(kvp("class"), kvp("style"), kvp("custom")),
                        defaultAvailableDescriptors,
                        new[] { divDescriptor, catchAllDescriptor, catchAllDescriptor2 }
                    },
                    {
                        "input",
                        ImmutableArray.Create(kvp("class"), kvp("style")),
                        defaultAvailableDescriptors,
                        new[] { inputDescriptor, catchAllDescriptor }
                    },
                    {
                        "input",
                        ImmutableArray.Create(kvp("nodashprefixA")),
                        defaultWildcardDescriptors,
                        new[] { inputWildcardPrefixDescriptor }
                    },
                    {
                        "input",
                        ImmutableArray.Create(kvp("nodashprefix-ABC-DEF"), kvp("random")),
                        defaultWildcardDescriptors,
                        new[] { inputWildcardPrefixDescriptor }
                    },
                    {
                        "input",
                        ImmutableArray.Create(kvp("prefixABCnodashprefix")),
                        defaultWildcardDescriptors,
                        null
                    },
                    {
                        "input",
                        ImmutableArray.Create(kvp("prefix-")),
                        defaultWildcardDescriptors,
                        null
                    },
                    {
                        "input",
                        ImmutableArray.Create(kvp("nodashprefix")),
                        defaultWildcardDescriptors,
                        null
                    },
                    {
                        "input",
                        ImmutableArray.Create(kvp("prefix-A")),
                        defaultWildcardDescriptors,
                        new[] { catchAllWildcardPrefixDescriptor }
                    },
                    {
                        "input",
                        ImmutableArray.Create(kvp("prefix-ABC-DEF"), kvp("random")),
                        defaultWildcardDescriptors,
                        new[] { catchAllWildcardPrefixDescriptor }
                    },
                    {
                        "input",
                        ImmutableArray.Create(kvp("prefix-abc"), kvp("nodashprefix-def")),
                        defaultWildcardDescriptors,
                        new[] { inputWildcardPrefixDescriptor, catchAllWildcardPrefixDescriptor }
                    },
                    {
                        "input",
                        ImmutableArray.Create(kvp("class"), kvp("prefix-abc"), kvp("onclick"), kvp("nodashprefix-def"), kvp("style")),
                        defaultWildcardDescriptors,
                        new[] { inputWildcardPrefixDescriptor, catchAllWildcardPrefixDescriptor }
                    },
                };
        }
    }

    [Theory]
    [MemberData(nameof(RequiredAttributeData))]
    public void GetBinding_ReturnsBindingResultDescriptorsWithRequiredAttributes(
        string tagName,
        ImmutableArray<KeyValuePair<string, string>> providedAttributes,
        object availableDescriptors,
        object expectedDescriptors)
    {
        // Arrange
        var tagHelperBinder = new TagHelperBinder(null, (IReadOnlyList<TagHelperDescriptor>)availableDescriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(tagName, providedAttributes, parentTagName: "p", parentIsTagHelper: false);

        // Assert
        Assert.Equal((IEnumerable<TagHelperDescriptor>)expectedDescriptors, bindingResult?.Descriptors, TagHelperDescriptorComparer.Default);
    }

    [Fact]
    public void GetBinding_ReturnsNullBindingResultPrefixAsTagName()
    {
        // Arrange
        var catchAllDescriptor = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName(TagHelperMatchingConventions.ElementCatchAllName))
            .Build();
        var descriptors = new[] { catchAllDescriptor };
        var tagHelperBinder = new TagHelperBinder("th", descriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "th",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        Assert.Null(bindingResult);
    }

    [Fact]
    public void GetBinding_ReturnsBindingResultCatchAllDescriptorsForPrefixedTags()
    {
        // Arrange
        var catchAllDescriptor = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName(TagHelperMatchingConventions.ElementCatchAllName))
            .Build();
        var descriptors = new[] { catchAllDescriptor };
        var tagHelperBinder = new TagHelperBinder("th:", descriptors);

        // Act
        var bindingResultDiv = tagHelperBinder.GetBinding(
            tagName: "th:div",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);
        var bindingResultSpan = tagHelperBinder.GetBinding(
            tagName: "th:span",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        var descriptor = Assert.Single(bindingResultDiv.Descriptors);
        Assert.Same(catchAllDescriptor, descriptor);
        descriptor = Assert.Single(bindingResultSpan.Descriptors);
        Assert.Same(catchAllDescriptor, descriptor);
    }

    [Fact]
    public void GetBinding_ReturnsBindingResultDescriptorsForPrefixedTags()
    {
        // Arrange
        var divDescriptor = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .Build();
        var descriptors = new[] { divDescriptor };
        var tagHelperBinder = new TagHelperBinder("th:", descriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "th:div",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        var descriptor = Assert.Single(bindingResult.Descriptors);
        Assert.Same(divDescriptor, descriptor);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("div")]
    public void GetBinding_ReturnsNullForUnprefixedTags(string tagName)
    {
        // Arrange
        var divDescriptor = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName(tagName))
            .Build();
        var descriptors = new[] { divDescriptor };
        var tagHelperBinder = new TagHelperBinder("th:", descriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "div",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        Assert.Null(bindingResult);
    }

    [Fact]
    public void GetDescriptors_ReturnsNothingForUnregisteredTags()
    {
        // Arrange
        var divDescriptor = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .Build();
        var spanDescriptor = TagHelperDescriptorBuilder.Create("foo2", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("span"))
            .Build();
        var descriptors = new TagHelperDescriptor[] { divDescriptor, spanDescriptor };
        var tagHelperBinder = new TagHelperBinder(null, descriptors);

        // Act
        var tagHelperBinding = tagHelperBinder.GetBinding(
            tagName: "foo",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        Assert.Null(tagHelperBinding);
    }

    [Fact]
    public void GetDescriptors_ReturnsCatchAllsWithEveryTagName()
    {
        // Arrange
        var divDescriptor = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .Build();
        var spanDescriptor = TagHelperDescriptorBuilder.Create("foo2", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("span"))
            .Build();
        var catchAllDescriptor = TagHelperDescriptorBuilder.Create("foo3", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName(TagHelperMatchingConventions.ElementCatchAllName))
            .Build();
        var descriptors = new TagHelperDescriptor[] { divDescriptor, spanDescriptor, catchAllDescriptor };
        var tagHelperBinder = new TagHelperBinder(null, descriptors);

        // Act
        var divBinding = tagHelperBinder.GetBinding(
            tagName: "div",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);
        var spanBinding = tagHelperBinder.GetBinding(
            tagName: "span",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        // For divs
        Assert.Equal(2, divBinding.Descriptors.Count());
        Assert.Contains(divDescriptor, divBinding.Descriptors);
        Assert.Contains(catchAllDescriptor, divBinding.Descriptors);

        // For spans
        Assert.Equal(2, spanBinding.Descriptors.Count());
        Assert.Contains(spanDescriptor, spanBinding.Descriptors);
        Assert.Contains(catchAllDescriptor, spanBinding.Descriptors);
    }

    [Fact]
    public void GetDescriptors_DuplicateDescriptorsAreNotPartOfTagHelperDescriptorPool()
    {
        // Arrange
        var divDescriptor = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .Build();
        var descriptors = new TagHelperDescriptor[] { divDescriptor, divDescriptor };
        var tagHelperBinder = new TagHelperBinder(null, descriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "div",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        var descriptor = Assert.Single(bindingResult.Descriptors);
        Assert.Same(divDescriptor, descriptor);
    }

    [Fact]
    public void GetBinding_DescriptorWithMultipleRules_CorrectlySelectsMatchingRules()
    {
        // Arrange
        var multiRuleDescriptor = TagHelperDescriptorBuilder.Create("foo", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule
                .RequireTagName(TagHelperMatchingConventions.ElementCatchAllName)
                .RequireParentTag("body"))
            .TagMatchingRuleDescriptor(rule => rule
                .RequireTagName("div"))
            .TagMatchingRuleDescriptor(rule => rule
                .RequireTagName("span"))
            .Build();
        var descriptors = new TagHelperDescriptor[] { multiRuleDescriptor };
        var tagHelperBinder = new TagHelperBinder(null, descriptors);

        // Act
        var binding = tagHelperBinder.GetBinding(
            tagName: "div",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        var boundDescriptor = Assert.Single(binding.Descriptors);
        Assert.Same(multiRuleDescriptor, boundDescriptor);
        var boundRules = binding.Mappings[boundDescriptor];
        var boundRule = Assert.Single(boundRules);
        Assert.Equal("div", boundRule.TagName);
    }

    [Fact]
    public void GetBinding_PrefixedParent_ReturnsBinding()
    {
        // Arrange
        var divDescriptor = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div").RequireParentTag("p"))
            .Build();
        var pDescriptor = TagHelperDescriptorBuilder.Create("foo2", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("p"))
            .Build();
        var descriptors = new[] { divDescriptor, pDescriptor };
        var tagHelperBinder = new TagHelperBinder("th:", descriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "th:div",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "th:p",
            parentIsTagHelper: true);

        // Assert
        var boundDescriptor = Assert.Single(bindingResult.Descriptors);
        Assert.Same(divDescriptor, boundDescriptor);
        var boundRules = bindingResult.Mappings[boundDescriptor];
        var boundRule = Assert.Single(boundRules);
        Assert.Equal("div", boundRule.TagName);
        Assert.Equal("p", boundRule.ParentTag);
    }

    [Fact]
    public void GetBinding_IsAttributeMatch_SingleAttributeMatch()
    {
        // Arrange
        var divDescriptor = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .Metadata(MakeTrue(TagHelperMetadata.Common.ClassifyAttributesOnly))
            .Build();

        var descriptors = new[] { divDescriptor, };
        var tagHelperBinder = new TagHelperBinder("", descriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "div",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        Assert.True(bindingResult.IsAttributeMatch);
    }

    [Fact]
    public void GetBinding_IsAttributeMatch_MultipleAttributeMatches()
    {
        // Arrange
        var divDescriptor1 = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .Metadata(MakeTrue(TagHelperMetadata.Common.ClassifyAttributesOnly))
            .Build();

        var divDescriptor2 = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .Metadata(MakeTrue(TagHelperMetadata.Common.ClassifyAttributesOnly))
            .Build();

        var descriptors = new[] { divDescriptor1, divDescriptor2, };
        var tagHelperBinder = new TagHelperBinder("", descriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "div",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        Assert.True(bindingResult.IsAttributeMatch);
    }

    [Fact]
    public void GetBinding_IsAttributeMatch_MixedAttributeMatches()
    {
        // Arrange
        var divDescriptor1 = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .Metadata(MakeTrue(TagHelperMetadata.Common.ClassifyAttributesOnly))
            .Build();

        var divDescriptor2 = TagHelperDescriptorBuilder.Create("foo1", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .Build();

        var descriptors = new[] { divDescriptor1, divDescriptor2, };
        var tagHelperBinder = new TagHelperBinder("", descriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "div",
            attributes: ImmutableArray<KeyValuePair<string, string>>.Empty,
            parentTagName: "p",
            parentIsTagHelper: false);

        // Assert
        Assert.False(bindingResult.IsAttributeMatch);
    }

    [Fact]
    public void GetBinding_CaseSensitiveRule_CaseMismatch_ReturnsNull()
    {
        // Arrange
        var divTagHelper = TagHelperDescriptorBuilder.Create("DivTagHelper", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("div"))
            .SetCaseSensitive()
            .Build();
        var expectedDescriptors = new[] { divTagHelper };
        var expectedAttributes = ImmutableArray.Create(
            new KeyValuePair<string, string>("class", "something"));
        var tagHelperBinder = new TagHelperBinder("th:", expectedDescriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "th:Div",
            attributes: expectedAttributes,
            parentTagName: "body",
            parentIsTagHelper: false);

        // Assert
        Assert.Null(bindingResult);
    }

    [Fact]
    public void GetBinding_CaseSensitiveRequiredAttribute_CaseMismatch_ReturnsNull()
    {
        // Arrange
        var divTagHelper = TagHelperDescriptorBuilder.Create("DivTagHelper", "SomeAssembly")
            .TagMatchingRuleDescriptor(rule => rule
                .RequireTagName("div")
                .RequireAttributeDescriptor(attribute => attribute.Name("class")))
            .SetCaseSensitive()
            .Build();
        var expectedDescriptors = new[] { divTagHelper };
        var expectedAttributes = ImmutableArray.Create(
            new KeyValuePair<string, string>("CLASS", "something"));
        var tagHelperBinder = new TagHelperBinder(null, expectedDescriptors);

        // Act
        var bindingResult = tagHelperBinder.GetBinding(
            tagName: "div",
            attributes: expectedAttributes,
            parentTagName: "body",
            parentIsTagHelper: false);

        // Assert
        Assert.Null(bindingResult);
    }
}
