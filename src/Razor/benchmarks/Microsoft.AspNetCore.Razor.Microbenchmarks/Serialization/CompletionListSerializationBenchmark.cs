﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.Serialization;

public class CompletionListSerializationBenchmark
{
    private readonly byte[] _completionListBuffer;

    private readonly CompletionList _completionList;

    public CompletionListSerializationBenchmark()
    {
        var completionService = new TagHelperCompletionService();
        var tagHelperCompletionProvider = new TagHelperCompletionProvider(completionService);

        var documentContent = "<";
        var queryIndex = 1;
        _completionList = GenerateCompletionList(documentContent, queryIndex, tagHelperCompletionProvider);
        _completionListBuffer = GenerateBuffer(_completionList);
    }

    [Benchmark(Description = "Component Completion List Roundtrip Serialization")]
    public void ComponentElement_CompletionList_Serialization_RoundTrip()
    {
        // Serialize back to json.
        MemoryStream originalStream;
        using (originalStream = new MemoryStream())
        {
            JsonSerializer.Serialize(originalStream, _completionList);
        }

        CompletionList deserializedCompletions;
        var stream = new MemoryStream(originalStream.GetBuffer());
        using (stream)
        {
            deserializedCompletions = JsonSerializer.Deserialize<CompletionList>(stream).AssumeNotNull();
        }
    }

    [Benchmark(Description = "Component Completion List Serialization")]
    public void ComponentElement_CompletionList_Serialization()
    {
        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, _completionList);
    }

    [Benchmark(Description = "Component Completion List Deserialization")]
    public void ComponentElement_CompletionList_Deserialization()
    {
        // Deserialize from json file.
        using var stream = new MemoryStream(_completionListBuffer);
        CompletionList deserializedCompletions;
        deserializedCompletions = JsonSerializer.Deserialize<CompletionList>(stream).AssumeNotNull();
    }

    private CompletionList GenerateCompletionList(string documentContent, int queryIndex, TagHelperCompletionProvider componentCompletionProvider)
    {
        var sourceDocument = RazorSourceDocument.Create(documentContent, RazorSourceDocumentProperties.Default);
        var syntaxTree = RazorSyntaxTree.Parse(sourceDocument);
        var tagHelperDocumentContext = TagHelperDocumentContext.Create(prefix: string.Empty, CommonResources.LegacyTagHelpers);

        var owner = syntaxTree.Root.FindInnermostNode(queryIndex, includeWhitespace: true, walkMarkersBack: true);
        var context = new RazorCompletionContext(queryIndex, owner, syntaxTree, tagHelperDocumentContext);

        var razorCompletionItems = componentCompletionProvider.GetCompletionItems(context);
        var completionList = RazorCompletionListProvider.CreateLSPCompletionList(
            razorCompletionItems,
            new VSInternalClientCapabilities()
            {
                TextDocument = new TextDocumentClientCapabilities()
                {
                    Completion = new VSInternalCompletionSetting()
                    {
                        CompletionItemKind = new CompletionItemKindSetting()
                        {
                            ValueSet = new[] { CompletionItemKind.TagHelper }
                        },
                        CompletionList = new VSInternalCompletionListSetting()
                        {
                            CommitCharacters = true,
                            Data = true,
                        }
                    }
                }
            });
        return completionList;
    }

    private byte[] GenerateBuffer(CompletionList completionList)
    {
        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, completionList);
        var buffer = stream.GetBuffer();

        return buffer;
    }
}
