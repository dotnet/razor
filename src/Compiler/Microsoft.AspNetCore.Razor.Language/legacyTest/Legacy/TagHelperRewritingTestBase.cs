﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

public class TagHelperRewritingTestBase() : ParserTestBase(layer: TestProject.Layer.Compiler, validateSpanEditHandlers: true, useLegacyTokenizer: true)
{
    internal void RunParseTreeRewriterTest(string documentContent, params string[] tagNames)
    {
        var descriptors = BuildDescriptors(tagNames);

        EvaluateData(descriptors, documentContent);
    }

    internal ImmutableArray<TagHelperDescriptor> BuildDescriptors(params string[] tagNames)
    {
        var descriptors = new List<TagHelperDescriptor>();

        foreach (var tagName in tagNames)
        {
            var descriptor = TagHelperDescriptorBuilder.Create(tagName + "taghelper", "SomeAssembly")
                .TagMatchingRuleDescriptor(rule => rule.RequireTagName(tagName))
                .Build();
            descriptors.Add(descriptor);
        }

        return descriptors.ToImmutableArray();
    }

    internal void EvaluateData(
        ImmutableArray<TagHelperDescriptor> descriptors,
        string documentContent,
        string tagHelperPrefix = null,
        RazorParserFeatureFlags featureFlags = null)
    {
        var syntaxTree = ParseDocument(documentContent, featureFlags: featureFlags);

        var binder = new TagHelperBinder(tagHelperPrefix, descriptors);
        var rewrittenTree = TagHelperParseTreeRewriter.Rewrite(syntaxTree, binder, out _);

        Assert.Equal(syntaxTree.Root.FullWidth, rewrittenTree.Root.FullWidth);

        BaselineTest(rewrittenTree);
    }
}
