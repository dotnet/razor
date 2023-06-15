// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class RazorCodeDocumentSerializer
{
    private const string TagHelperContext = nameof(TagHelperContext);
    private const string ParserOptions = nameof(ParserOptions);
    private const string SyntaxTree = nameof(SyntaxTree);
    private const string DocumentIntermediateNode = nameof(DocumentIntermediateNode);
    private const string FileKind = nameof(FileKind);
    private const string CssScope = nameof(CssScope);
    private const string CSharpDocument = nameof(CSharpDocument);
    private const string HtmlDocument = nameof(HtmlDocument);
    private const string Namespace = nameof(Namespace);

    private readonly JsonSerializer _serializer;

    public static readonly RazorCodeDocumentSerializer Instance = new(Formatting.None);

    // internal for testing
    internal RazorCodeDocumentSerializer(Formatting formatting)
    {
        _serializer = new JsonSerializer
        {
            Formatting = formatting,
            Converters =
            {
                RazorDiagnosticJsonConverter.Instance,
                TagHelperDescriptorJsonConverter.Instance,
                new EncodingConverter(),
                new RazorCodeGenerationOptionsConverter(),
                new SourceSpanConverter(),
                new RazorParserOptionsConverter(),
                new DirectiveDescriptorConverter(),
                new DirectiveTokenDescriptorConverter(),
                new ItemCollectionConverter(),
                new RazorSourceDocumentConverter(),
            },
            ContractResolver = new RazorContractResolver(),
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = new RazorSerializationBinder(),
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
        };
    }

    public RazorCodeDocument? Deserialize(string json, RazorSourceDocument source)
    {
        using var textReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(textReader);
        return Deserialize(jsonReader, source);
    }

    public RazorCodeDocument? Deserialize(JsonReader reader, RazorSourceDocument source)
    {
        if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }

        var document = RazorCodeDocument.Create(source);

        reader.ReadProperties(propertyName =>
        {
            switch (propertyName)
            {
                case nameof(FileKind):
                    document.SetFileKind(reader.ReadAsString());
                    break;
                case nameof(CssScope):
                    document.SetCssScope(reader.ReadAsString());
                    break;
                case nameof(TagHelperContext):
                    if (reader.Read() && reader.TokenType == JsonToken.StartObject)
                    {
                        string? prefix = null;
                        IReadOnlyList<TagHelperDescriptor>? tagHelpers = null;
                        reader.ReadProperties(propertyName =>
                        {
                            switch (propertyName)
                            {
                                case nameof(TagHelperDocumentContext.Prefix):
                                    reader.Read();
                                    prefix = (string?)reader.Value;
                                    break;
                                case nameof(TagHelperDocumentContext.TagHelpers):
                                    reader.Read();
                                    tagHelpers = _serializer.Deserialize<IReadOnlyList<TagHelperDescriptor>?>(reader);
                                    break;
                            }
                        });
                        if (tagHelpers != null)
                        {
                            document.SetTagHelperContext(TagHelperDocumentContext.Create(prefix, tagHelpers));
                        }
                    }
                    break;
                case nameof(ParserOptions):
                    reader.Read();
                    document.SetParserOptions(_serializer.Deserialize<RazorParserOptions>(reader));
                    break;
                case nameof(DocumentIntermediateNode):
                    reader.Read();
                    document.SetDocumentIntermediateNode(_serializer.Deserialize<DocumentIntermediateNode>(reader));
                    break;
                case nameof(SyntaxTree):
                    if (reader.Read() && DeserializeSyntaxTree(reader, document) is { } syntaxTree)
                    {
                        document.SetSyntaxTree(syntaxTree);
                    }
                    break;
                case nameof(CSharpDocument):
                    if (reader.Read() && DeserializeCSharpDocument(reader, document) is { } cSharpDocument)
                    {
                        document.SetCSharpDocument(cSharpDocument);
                    }
                    break;
                case nameof(HtmlDocument):
                    if (reader.Read() && DeserializeHtmlDocument(reader, document) is { } htmlDocument)
                    {
                        document.Items[typeof(RazorHtmlDocument)] = htmlDocument;
                    }
                    break;
                case nameof(Namespace):
                    document.SetNamespace(reader.ReadAsString());
                    break;
            }
        });

        return document;
    }

    public string Serialize(RazorCodeDocument? document)
    {
        using var textWriter = new StringWriter();
        using var jsonWriter = new JsonTextWriter(textWriter);
        Serialize(jsonWriter, document);
        return textWriter.ToString();
    }

    public void Serialize(JsonWriter writer, RazorCodeDocument? document)
    {
        if (document == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();

        if (document.GetFileKind() is { } fileKind)
        {
            writer.WritePropertyName(nameof(FileKind));
            writer.WriteValue(fileKind);
        }

        if (document.GetCssScope() is { } cssScope)
        {
            writer.WritePropertyName(nameof(CssScope));
            writer.WriteValue(cssScope);
        }

        if (document.GetDocumentIntermediateNode() is { } intermediateNode)
        {
            writer.WritePropertyName(DocumentIntermediateNode);
            _serializer.Serialize(writer, intermediateNode);
        }

        if (document.GetTagHelperContext() is { } tagHelperContext)
        {
            writer.WritePropertyName(TagHelperContext);
            writer.WriteStartObject();

            if (tagHelperContext.Prefix is { } prefix)
            {
                writer.WritePropertyName(nameof(TagHelperDocumentContext.Prefix));
                writer.WriteValue(prefix);
            }

            if (tagHelperContext.TagHelpers is { Count: > 0 } tagHelpers)
            {
                writer.WritePropertyName(nameof(TagHelperDocumentContext.TagHelpers));
                _serializer.Serialize(writer, tagHelpers);
            }

            writer.WriteEndObject();
        }

        if (document.GetSyntaxTree() is { } syntaxTree)
        {
            writer.WritePropertyName(SyntaxTree);
            SerializeSyntaxTree(writer, document, syntaxTree);
        }

        if (document.GetCSharpDocument() is { } cSharpDocument)
        {
            writer.WritePropertyName(CSharpDocument);
            SerializeCSharpDocument(writer, cSharpDocument);
        }

        if (document.GetHtmlDocument() is { } htmlDocument)
        {
            writer.WritePropertyName(HtmlDocument);
            SerializeHtmlDocument(writer, htmlDocument);
        }

        document.TryComputeNamespace(fallbackToRootNamespace: true, check: false, out var @namespace);
        writer.WritePropertyName(Namespace);
        writer.WriteValue(@namespace);

        writer.WriteEndObject();
    }

    private void SerializeSyntaxTree(JsonWriter writer, RazorCodeDocument owner, RazorSyntaxTree syntaxTree)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(nameof(RazorSyntaxTree.Options));
        _serializer.Serialize(writer, syntaxTree.Options);

        if (syntaxTree.Source != owner.Source)
        {
            writer.WritePropertyName(nameof(RazorSyntaxTree.Source));
            _serializer.Serialize(writer, syntaxTree.Source);
        }

        writer.WriteEndObject();
    }

    private RazorSyntaxTree? DeserializeSyntaxTree(JsonReader reader, RazorCodeDocument owner)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }

        RazorParserOptions? options = null;
        RazorSourceDocument source = owner.Source;
        reader.ReadProperties(propertyName =>
        {
            switch (propertyName)
            {
                case nameof(RazorSyntaxTree.Options):
                    reader.Read();
                    options = _serializer.Deserialize<RazorParserOptions>(reader);
                    break;
                case nameof(RazorSyntaxTree.Source):
                    reader.Read();
                    source = _serializer.Deserialize<RazorSourceDocument>(reader)!;
                    break;
            }
        });
        return RazorSyntaxTree.Parse(source, options);
    }

    private void SerializeCSharpDocument(JsonWriter writer, RazorCSharpDocument document)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(nameof(RazorCSharpDocument.GeneratedCode));
        writer.WriteValue(document.GeneratedCode);
        writer.WritePropertyName(nameof(RazorCSharpDocument.Options));
        _serializer.Serialize(writer, document.Options);
        writer.WritePropertyName(nameof(RazorCSharpDocument.Diagnostics));
        _serializer.Serialize(writer, document.Diagnostics);
        writer.WritePropertyName(nameof(RazorCSharpDocument.SourceMappings));
        _serializer.Serialize(writer, document.SourceMappings);
        writer.WritePropertyName(nameof(RazorCSharpDocument.LinePragmas));
        _serializer.Serialize(writer, document.LinePragmas);
        writer.WriteEndObject();
    }

    private RazorCSharpDocument? DeserializeCSharpDocument(JsonReader reader, RazorCodeDocument owner)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }

        string? generatedCode = null;
        RazorCodeGenerationOptions? options = null;
        IReadOnlyList<RazorDiagnostic>? diagnostics = null;
        IReadOnlyList<SourceMapping>? sourceMappings = null;
        IReadOnlyList<LinePragma>? linePragmas = null;
        reader.ReadProperties((propertyName) =>
        {
            switch (propertyName)
            {
                case nameof(RazorCSharpDocument.GeneratedCode):
                    generatedCode = reader.ReadAsString();
                    break;
                case nameof(RazorCSharpDocument.Options):
                    reader.Read();
                    options = _serializer.Deserialize<RazorCodeGenerationOptions>(reader);
                    break;
                case nameof(RazorCSharpDocument.Diagnostics):
                    reader.Read();
                    diagnostics = _serializer.Deserialize<IReadOnlyList<RazorDiagnostic>>(reader);
                    break;
                case nameof(RazorCSharpDocument.SourceMappings):
                    reader.Read();
                    sourceMappings = _serializer.Deserialize<IReadOnlyList<SourceMapping>>(reader);
                    break;
                case nameof(RazorCSharpDocument.LinePragmas):
                    reader.Read();
                    linePragmas = _serializer.Deserialize<IReadOnlyList<LinePragma>>(reader);
                    break;
            }
        });
        return RazorCSharpDocument.Create(owner, generatedCode, options, diagnostics, sourceMappings, linePragmas);
    }

    private void SerializeHtmlDocument(JsonWriter writer, RazorHtmlDocument document)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(nameof(RazorHtmlDocument.GeneratedCode));
        writer.WriteValue(document.GeneratedCode);
        writer.WritePropertyName(nameof(RazorHtmlDocument.Options));
        _serializer.Serialize(writer, document.Options);
        writer.WritePropertyName(nameof(RazorHtmlDocument.SourceMappings));
        _serializer.Serialize(writer, document.SourceMappings);
        writer.WriteEndObject();
    }

    private RazorHtmlDocument? DeserializeHtmlDocument(JsonReader reader, RazorCodeDocument owner)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }

        string? generatedCode = null;
        RazorCodeGenerationOptions? options = null;
        SourceMapping[]? sourceMappings = null;
        reader.ReadProperties((propertyName) =>
        {
            switch (propertyName)
            {
                case nameof(RazorCSharpDocument.GeneratedCode):
                    generatedCode = reader.ReadAsString();
                    break;
                case nameof(RazorCSharpDocument.Options):
                    reader.Read();
                    options = _serializer.Deserialize<RazorCodeGenerationOptions>(reader);
                    break;
                case nameof(RazorCSharpDocument.SourceMappings):
                    reader.Read();
                    sourceMappings = _serializer.Deserialize<SourceMapping[]>(reader);
                    break;
            }
        });
        return RazorHtmlDocument.Create(owner, generatedCode, options, sourceMappings);
    }
}
