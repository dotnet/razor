﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Serialization.Internal;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Razor.Serialization
{
    internal class TagHelperDescriptorJsonConverter : JsonConverter
    {
        public static readonly TagHelperDescriptorJsonConverter Instance = new TagHelperDescriptorJsonConverter();

        private static readonly StringCache s_stringCache = new StringCache();

        public static bool DisableCachingForTesting { private get; set; } = false;

        public override bool CanConvert(Type objectType)
        {
            return typeof(TagHelperDescriptor).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartObject)
            {
                return null;
            }

            // Try reading the optional hashcode
            // Note; the JsonReader will read a numeric value as a Int64 (long) by default
            var hashWasRead = reader.TryReadNextProperty<long>(RazorSerializationConstants.HashCodePropertyName, out var hashLong);
            var hash = hashWasRead ? Convert.ToInt32(hashLong) : 0;
            if (!DisableCachingForTesting &&
                hashWasRead &&
                TagHelperDescriptorCache.TryGetDescriptor(hash, out var descriptor))
            {
                ReadToEndOfCurrentObject(reader);
                return descriptor;
            }

            // Required tokens (order matters)
            if (!reader.TryReadNextProperty<string>(nameof(TagHelperDescriptor.Kind), out var descriptorKind))
            {
                return default;
            }

            if (!reader.TryReadNextProperty<string>(nameof(TagHelperDescriptor.Name), out var typeName))
            {
                return default;
            }

            if (!reader.TryReadNextProperty<string>(nameof(TagHelperDescriptor.AssemblyName), out var assemblyName))
            {
                return default;
            }

            var builder = TagHelperDescriptorBuilder.Create(Cached(descriptorKind), Cached(typeName), Cached(assemblyName));

            reader.ReadProperties(static (propertyName, arg) =>
            {
                var (reader, builder) = (arg.reader, arg.builder);
                switch (propertyName)
                {
                    case nameof(TagHelperDescriptor.Documentation):
                        if (reader.Read())
                        {
                            var documentation = (string)reader.Value;
                            builder.Documentation = Cached(documentation);
                        }

                        break;
                    case nameof(TagHelperDescriptor.TagOutputHint):
                        if (reader.Read())
                        {
                            var tagOutputHint = (string)reader.Value;
                            // TODO: Needed?
                            builder.TagOutputHint = Cached(tagOutputHint);
                        }

                        break;
                    case nameof(TagHelperDescriptor.CaseSensitive):
                        if (reader.Read())
                        {
                            var caseSensitive = (bool)reader.Value;
                            builder.CaseSensitive = caseSensitive;
                        }

                        break;
                    case nameof(TagHelperDescriptor.TagMatchingRules):
                        ReadTagMatchingRules(reader, builder);
                        break;
                    case nameof(TagHelperDescriptor.BoundAttributes):
                        ReadBoundAttributes(reader, builder);
                        break;
                    case nameof(TagHelperDescriptor.AllowedChildTags):
                        ReadAllowedChildTags(reader, builder);
                        break;
                    case nameof(TagHelperDescriptor.Diagnostics):
                        ReadDiagnostics(reader, builder.Diagnostics);
                        break;
                    case nameof(TagHelperDescriptor.Metadata):
                        ReadMetadata(reader, builder.Metadata);
                        break;
                }
            }, (reader, builder));

            descriptor = builder.Build();
            if (!DisableCachingForTesting && hashWasRead)
            {
                TagHelperDescriptorCache.Set(hash, descriptor);
            }

            return descriptor;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var tagHelper = (TagHelperDescriptor)value;

            writer.WriteStartObject();

            writer.WritePropertyName(RazorSerializationConstants.HashCodePropertyName);
            writer.WriteValue(tagHelper.GetHashCode());

            writer.WritePropertyName(nameof(TagHelperDescriptor.Kind));
            writer.WriteValue(tagHelper.Kind);

            writer.WritePropertyName(nameof(TagHelperDescriptor.Name));
            writer.WriteValue(tagHelper.Name);

            writer.WritePropertyName(nameof(TagHelperDescriptor.AssemblyName));
            writer.WriteValue(tagHelper.AssemblyName);

            if (tagHelper.Documentation != null)
            {
                writer.WritePropertyName(nameof(TagHelperDescriptor.Documentation));
                writer.WriteValue(tagHelper.Documentation);
            }

            if (tagHelper.TagOutputHint != null)
            {
                writer.WritePropertyName(nameof(TagHelperDescriptor.TagOutputHint));
                writer.WriteValue(tagHelper.TagOutputHint);
            }

            writer.WritePropertyName(nameof(TagHelperDescriptor.CaseSensitive));
            writer.WriteValue(tagHelper.CaseSensitive);

            writer.WritePropertyName(nameof(TagHelperDescriptor.TagMatchingRules));
            writer.WriteStartArray();
            foreach (var ruleDescriptor in tagHelper.TagMatchingRules)
            {
                WriteTagMatchingRule(writer, ruleDescriptor, serializer);
            }

            writer.WriteEndArray();

            if (tagHelper.BoundAttributes != null && tagHelper.BoundAttributes.Count > 0)
            {
                writer.WritePropertyName(nameof(TagHelperDescriptor.BoundAttributes));
                writer.WriteStartArray();
                foreach (var boundAttribute in tagHelper.BoundAttributes)
                {
                    WriteBoundAttribute(writer, boundAttribute, serializer);
                }

                writer.WriteEndArray();
            }

            if (tagHelper.AllowedChildTags != null && tagHelper.AllowedChildTags.Count > 0)
            {
                writer.WritePropertyName(nameof(TagHelperDescriptor.AllowedChildTags));
                writer.WriteStartArray();
                foreach (var allowedChildTag in tagHelper.AllowedChildTags)
                {
                    WriteAllowedChildTags(writer, allowedChildTag, serializer);
                }

                writer.WriteEndArray();
            }

            if (tagHelper.Diagnostics != null && tagHelper.Diagnostics.Count > 0)
            {
                writer.WritePropertyName(nameof(TagHelperDescriptor.Diagnostics));
                serializer.Serialize(writer, tagHelper.Diagnostics);
            }

            writer.WritePropertyName(nameof(TagHelperDescriptor.Metadata));
            WriteMetadata(writer, tagHelper.Metadata);

            writer.WriteEndObject();
        }

        private static void WriteAllowedChildTags(JsonWriter writer, AllowedChildTagDescriptor allowedChildTag, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(nameof(AllowedChildTagDescriptor.Name));
            writer.WriteValue(allowedChildTag.Name);

            writer.WritePropertyName(nameof(AllowedChildTagDescriptor.DisplayName));
            writer.WriteValue(allowedChildTag.DisplayName);

            writer.WritePropertyName(nameof(AllowedChildTagDescriptor.Diagnostics));
            serializer.Serialize(writer, allowedChildTag.Diagnostics);

            writer.WriteEndObject();
        }

        private static void WriteBoundAttribute(JsonWriter writer, BoundAttributeDescriptor boundAttribute, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(nameof(BoundAttributeDescriptor.Kind));
            writer.WriteValue(boundAttribute.Kind);

            writer.WritePropertyName(nameof(BoundAttributeDescriptor.Name));
            writer.WriteValue(boundAttribute.Name);

            writer.WritePropertyName(nameof(BoundAttributeDescriptor.TypeName));
            writer.WriteValue(boundAttribute.TypeName);

            if (boundAttribute.IsEnum)
            {
                writer.WritePropertyName(nameof(BoundAttributeDescriptor.IsEnum));
                writer.WriteValue(boundAttribute.IsEnum);
            }

            if (boundAttribute.IsEditorRequired)
            {
                writer.WritePropertyName(nameof(BoundAttributeDescriptor.IsEditorRequired));
                writer.WriteValue(true);
            }

            if (boundAttribute.IndexerNamePrefix != null)
            {
                writer.WritePropertyName(nameof(BoundAttributeDescriptor.IndexerNamePrefix));
                writer.WriteValue(boundAttribute.IndexerNamePrefix);
            }

            if (boundAttribute.IndexerTypeName != null)
            {
                writer.WritePropertyName(nameof(BoundAttributeDescriptor.IndexerTypeName));
                writer.WriteValue(boundAttribute.IndexerTypeName);
            }

            if (boundAttribute.Documentation != null)
            {
                writer.WritePropertyName(nameof(BoundAttributeDescriptor.Documentation));
                writer.WriteValue(boundAttribute.Documentation);
            }

            if (boundAttribute.Diagnostics != null && boundAttribute.Diagnostics.Count > 0)
            {
                writer.WritePropertyName(nameof(BoundAttributeDescriptor.Diagnostics));
                serializer.Serialize(writer, boundAttribute.Diagnostics);
            }

            writer.WritePropertyName(nameof(BoundAttributeDescriptor.Metadata));
            WriteMetadata(writer, boundAttribute.Metadata);

            if (boundAttribute.BoundAttributeParameters != null && boundAttribute.BoundAttributeParameters.Count > 0)
            {
                writer.WritePropertyName(nameof(BoundAttributeDescriptor.BoundAttributeParameters));
                writer.WriteStartArray();
                foreach (var boundAttributeParameter in boundAttribute.BoundAttributeParameters)
                {
                    WriteBoundAttributeParameter(writer, boundAttributeParameter, serializer);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        private static void WriteBoundAttributeParameter(JsonWriter writer, BoundAttributeParameterDescriptor boundAttributeParameter, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.Name));
            writer.WriteValue(boundAttributeParameter.Name);

            writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.TypeName));
            writer.WriteValue(boundAttributeParameter.TypeName);

            if (boundAttributeParameter.IsEnum != default)
            {
                writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.IsEnum));
                writer.WriteValue(boundAttributeParameter.IsEnum);
            }

            if (boundAttributeParameter.Documentation != null)
            {
                writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.Documentation));
                writer.WriteValue(boundAttributeParameter.Documentation);
            }

            if (boundAttributeParameter.Diagnostics != null && boundAttributeParameter.Diagnostics.Count > 0)
            {
                writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.Diagnostics));
                serializer.Serialize(writer, boundAttributeParameter.Diagnostics);
            }

            writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.Metadata));
            WriteMetadata(writer, boundAttributeParameter.Metadata);

            writer.WriteEndObject();
        }

        private static void WriteMetadata(JsonWriter writer, IReadOnlyDictionary<string, string> metadata)
        {
            writer.WriteStartObject();
            foreach (var kvp in metadata)
            {
                writer.WritePropertyName(kvp.Key);
                writer.WriteValue(kvp.Value);
            }

            writer.WriteEndObject();
        }

        private static void WriteTagMatchingRule(JsonWriter writer, TagMatchingRuleDescriptor ruleDescriptor, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.TagName));
            writer.WriteValue(ruleDescriptor.TagName);

            if (ruleDescriptor.ParentTag != null)
            {
                writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.ParentTag));
                writer.WriteValue(ruleDescriptor.ParentTag);
            }

            if (ruleDescriptor.TagStructure != default)
            {
                writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.TagStructure));
                writer.WriteValue(ruleDescriptor.TagStructure);
            }

            if (ruleDescriptor.Attributes != null && ruleDescriptor.Attributes.Count > 0)
            {
                writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.Attributes));
                writer.WriteStartArray();
                foreach (var requiredAttribute in ruleDescriptor.Attributes)
                {
                    WriteRequiredAttribute(writer, requiredAttribute, serializer);
                }

                writer.WriteEndArray();
            }

            if (ruleDescriptor.Diagnostics != null && ruleDescriptor.Diagnostics.Count > 0)
            {
                writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.Diagnostics));
                serializer.Serialize(writer, ruleDescriptor.Diagnostics);
            }

            writer.WriteEndObject();
        }

        private static void WriteRequiredAttribute(JsonWriter writer, RequiredAttributeDescriptor requiredAttribute, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(nameof(RequiredAttributeDescriptor.Name));
            writer.WriteValue(requiredAttribute.Name);

            if (requiredAttribute.NameComparison != default)
            {
                writer.WritePropertyName(nameof(RequiredAttributeDescriptor.NameComparison));
                writer.WriteValue(requiredAttribute.NameComparison);
            }

            if (requiredAttribute.Value != null)
            {
                writer.WritePropertyName(nameof(RequiredAttributeDescriptor.Value));
                writer.WriteValue(requiredAttribute.Value);
            }

            if (requiredAttribute.ValueComparison != default)
            {
                writer.WritePropertyName(nameof(RequiredAttributeDescriptor.ValueComparison));
                writer.WriteValue(requiredAttribute.ValueComparison);
            }

            if (requiredAttribute.Diagnostics != null && requiredAttribute.Diagnostics.Count > 0)
            {
                writer.WritePropertyName(nameof(RequiredAttributeDescriptor.Diagnostics));
                serializer.Serialize(writer, requiredAttribute.Diagnostics);
            }

            if (requiredAttribute.Metadata != null && requiredAttribute.Metadata.Count > 0)
            {
                writer.WritePropertyName(nameof(RequiredAttributeDescriptor.Metadata));
                WriteMetadata(writer, requiredAttribute.Metadata);
            }

            writer.WriteEndObject();
        }

        private static void ReadBoundAttributes(JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartArray)
            {
                return;
            }

            do
            {
                ReadBoundAttribute(reader, builder);
            } while (reader.TokenType != JsonToken.EndArray);
        }

        private static void ReadBoundAttribute(JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                return;
            }

            builder.BindAttribute(attribute => reader.ReadProperties(static (propertyName, arg) =>
            {
                var (reader, attribute) = (arg.reader, arg.attribute);
                switch (propertyName)
                {
                    case nameof(BoundAttributeDescriptor.Name):
                        if (reader.Read())
                        {
                            var name = (string)reader.Value;
                            attribute.Name = Cached(name);
                        }

                        break;
                    case nameof(BoundAttributeDescriptor.TypeName):
                        if (reader.Read())
                        {
                            var typeName = (string)reader.Value;
                            attribute.TypeName = Cached(typeName);
                        }

                        break;
                    case nameof(BoundAttributeDescriptor.Documentation):
                        if (reader.Read())
                        {
                            var documentation = (string)reader.Value;
                            attribute.Documentation = Cached(documentation);
                        }

                        break;
                    case nameof(BoundAttributeDescriptor.IndexerNamePrefix):
                        if (reader.Read())
                        {
                            var indexerNamePrefix = (string)reader.Value;
                            if (indexerNamePrefix != null)
                            {
                                attribute.IsDictionary = true;
                                attribute.IndexerAttributeNamePrefix = Cached(indexerNamePrefix);
                            }
                        }

                        break;
                    case nameof(BoundAttributeDescriptor.IndexerTypeName):
                        if (reader.Read())
                        {
                            var indexerTypeName = (string)reader.Value;
                            if (indexerTypeName != null)
                            {
                                attribute.IsDictionary = true;
                                attribute.IndexerValueTypeName = Cached(indexerTypeName);
                            }
                        }

                        break;
                    case nameof(BoundAttributeDescriptor.IsEnum):
                        if (reader.Read())
                        {
                            var isEnum = (bool)reader.Value;
                            attribute.IsEnum = isEnum;
                        }

                        break;
                    case nameof(BoundAttributeDescriptor.BoundAttributeParameters):
                        ReadBoundAttributeParameters(reader, attribute);
                        break;
                    case nameof(BoundAttributeDescriptor.Diagnostics):
                        ReadDiagnostics(reader, attribute.Diagnostics);
                        break;
                    case nameof(BoundAttributeDescriptor.Metadata):
                        ReadMetadata(reader, attribute.Metadata);
                        break;
                    case nameof(BoundAttributeDescriptor.IsEditorRequired):
                        if (reader.Read())
                        {
                            attribute.IsEditorRequired = (bool)reader.Value;
                        }

                        break;
                }
            }, (reader, attribute)));
        }

        private static void ReadBoundAttributeParameters(JsonReader reader, BoundAttributeDescriptorBuilder builder)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartArray)
            {
                return;
            }

            do
            {
                ReadBoundAttributeParameter(reader, builder);
            } while (reader.TokenType != JsonToken.EndArray);
        }

        private static void ReadBoundAttributeParameter(JsonReader reader, BoundAttributeDescriptorBuilder builder)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                return;
            }

            builder.BindAttributeParameter(parameter => reader.ReadProperties(static (propertyName, arg) =>
            {
                var (reader, parameter) = (arg.reader, arg.parameter);
                switch (propertyName)
                {
                    case nameof(BoundAttributeParameterDescriptor.Name):
                        if (reader.Read())
                        {
                            var name = (string)reader.Value;
                            parameter.Name = Cached(name);
                        }

                        break;
                    case nameof(BoundAttributeParameterDescriptor.TypeName):
                        if (reader.Read())
                        {
                            var typeName = (string)reader.Value;
                            parameter.TypeName = Cached(typeName);
                        }

                        break;
                    case nameof(BoundAttributeParameterDescriptor.IsEnum):
                        if (reader.Read())
                        {
                            var isEnum = (bool)reader.Value;
                            parameter.IsEnum = isEnum;
                        }

                        break;
                    case nameof(BoundAttributeParameterDescriptor.Documentation):
                        if (reader.Read())
                        {
                            var documentation = (string)reader.Value;
                            parameter.Documentation = Cached(documentation);
                        }

                        break;
                    case nameof(BoundAttributeParameterDescriptor.Metadata):
                        ReadMetadata(reader, parameter.Metadata);
                        break;
                    case nameof(BoundAttributeParameterDescriptor.Diagnostics):
                        ReadDiagnostics(reader, parameter.Diagnostics);
                        break;
                }
            }, (reader, parameter)));
        }

        private static void ReadTagMatchingRules(JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartArray)
            {
                return;
            }

            do
            {
                ReadTagMatchingRule(reader, builder);
            } while (reader.TokenType != JsonToken.EndArray);
        }

        private static void ReadTagMatchingRule(JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                return;
            }

            builder.TagMatchingRule(rule => reader.ReadProperties(static (propertyName, arg) =>
            {
                var (reader, rule) = (arg.reader, arg.rule);
                switch (propertyName)
                {
                    case nameof(TagMatchingRuleDescriptor.TagName):
                        if (reader.Read())
                        {
                            var tagName = (string)reader.Value;
                            rule.TagName = Cached(tagName);
                        }

                        break;
                    case nameof(TagMatchingRuleDescriptor.ParentTag):
                        if (reader.Read())
                        {
                            var parentTag = (string)reader.Value;
                            rule.ParentTag = Cached(parentTag);
                        }

                        break;
                    case nameof(TagMatchingRuleDescriptor.TagStructure):
                        rule.TagStructure = (TagStructure)reader.ReadAsInt32();
                        break;
                    case nameof(TagMatchingRuleDescriptor.Attributes):
                        ReadRequiredAttributeValues(reader, rule);
                        break;
                    case nameof(TagMatchingRuleDescriptor.Diagnostics):
                        ReadDiagnostics(reader, rule.Diagnostics);
                        break;
                }
            }, (reader, rule)));
        }

        private static void ReadRequiredAttributeValues(JsonReader reader, TagMatchingRuleDescriptorBuilder builder)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartArray)
            {
                return;
            }

            do
            {
                ReadRequiredAttribute(reader, builder);
            } while (reader.TokenType != JsonToken.EndArray);
        }

        private static void ReadRequiredAttribute(JsonReader reader, TagMatchingRuleDescriptorBuilder builder)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                return;
            }

            builder.Attribute(attribute => reader.ReadProperties(static (propertyName, arg) =>
            {
                var (reader, attribute) = (arg.reader, arg.attribute);
                switch (propertyName)
                {
                    case nameof(RequiredAttributeDescriptor.Name):
                        if (reader.Read())
                        {
                            var name = (string)reader.Value;
                            attribute.Name = Cached(name);
                        }

                        break;
                    case nameof(RequiredAttributeDescriptor.NameComparison):
                        var nameComparison = (RequiredAttributeDescriptor.NameComparisonMode)reader.ReadAsInt32();
                        attribute.NameComparisonMode = nameComparison;
                        break;
                    case nameof(RequiredAttributeDescriptor.Value):
                        if (reader.Read())
                        {
                            var value = (string)reader.Value;
                            attribute.Value = Cached(value);
                        }

                        break;
                    case nameof(RequiredAttributeDescriptor.ValueComparison):
                        var valueComparison = (RequiredAttributeDescriptor.ValueComparisonMode)reader.ReadAsInt32();
                        attribute.ValueComparisonMode = valueComparison;
                        break;
                    case nameof(RequiredAttributeDescriptor.Diagnostics):
                        ReadDiagnostics(reader, attribute.Diagnostics);
                        break;
                    case nameof(RequiredAttributeDescriptor.Metadata):
                        ReadMetadata(reader, attribute.Metadata);
                        break;
                }
            }, (reader, attribute)));
        }

        private static void ReadAllowedChildTags(JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartArray)
            {
                return;
            }

            do
            {
                ReadAllowedChildTag(reader, builder);
            } while (reader.TokenType != JsonToken.EndArray);
        }

        private static void ReadAllowedChildTag(JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                return;
            }

            builder.AllowChildTag(childTag => reader.ReadProperties(static (propertyName, arg) =>
            {
                var (reader, childTag) = (arg.reader, arg.childTag);
                switch (propertyName)
                {
                    case nameof(AllowedChildTagDescriptor.Name):
                        if (reader.Read())
                        {
                            var name = (string)reader.Value;
                            childTag.Name = Cached(name);
                        }

                        break;
                    case nameof(AllowedChildTagDescriptor.DisplayName):
                        if (reader.Read())
                        {
                            var displayName = (string)reader.Value;
                            childTag.DisplayName = Cached(displayName);
                        }

                        break;
                    case nameof(AllowedChildTagDescriptor.Diagnostics):
                        ReadDiagnostics(reader, childTag.Diagnostics);
                        break;
                }
            }, (reader, childTag)));
        }

        private static void ReadMetadata(JsonReader reader, IDictionary<string, string> metadata)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                return;
            }

            reader.ReadProperties(static (propertyName, arg) =>
            {
                var (reader, metadata) = (arg.reader, arg.metadata);
                if (reader.Read())
                {
                    var value = (string)reader.Value;
                    metadata[Cached(propertyName)] = Cached(value);
                }
            }, (reader, metadata));
        }

        private static void ReadDiagnostics(JsonReader reader, RazorDiagnosticCollection diagnostics)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartArray)
            {
                return;
            }

            do
            {
                ReadDiagnostic(reader, diagnostics);
            } while (reader.TokenType != JsonToken.EndArray);
        }

        private static void ReadDiagnostic(JsonReader reader, RazorDiagnosticCollection diagnostics)
        {
            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                return;
            }

            var (_, id, severity, message, sourceSpan) = reader.ReadProperties(static (propertyName, arg) =>
            {
                var (reader, id, severity, message, sourceSpan) = (arg.reader, arg.id, arg.severity, arg.message, arg.sourceSpan);
                switch (propertyName)
                {
                    case nameof(RazorDiagnostic.Id):
                        if (reader.Read())
                        {
                            id = (string)reader.Value;
                        }

                        break;
                    case nameof(RazorDiagnostic.Severity):
                        severity = reader.ReadAsInt32().Value;
                        break;
                    case "Message":
                        if (reader.Read())
                        {
                            message = (string)reader.Value;
                        }

                        break;
                    case nameof(RazorDiagnostic.Span):
                        sourceSpan = ReadSourceSpan(reader);
                        break;
                }

                return (reader, id, severity, message, sourceSpan);
            }, (reader, id: (string)null, severity: 0, message: (string)null, sourceSpan: default(SourceSpan)));

            var cachedMsg = Cached(message);
            var cachedId = Cached(id);
            var descriptor = CreateDiagnosticDescriptor(cachedId, cachedMsg, (RazorDiagnosticSeverity)severity);
            var diagnostic = RazorDiagnostic.Create(descriptor, sourceSpan);
            diagnostics.Add(diagnostic);

            static RazorDiagnosticDescriptor CreateDiagnosticDescriptor(string id, string message, RazorDiagnosticSeverity severity)
            {
                // Do NOT inline this descriptor factory method into the call site above.
                //
                // Reasoning:
                //  Because "message" is referenced in not only the below "() => message" but also the above switch statement containing lambda without this separate static method the
                //  compiler would generate a fake display class that looks something like:
                //  private sealed class <>c__DisplayClass26_0
                //  {
                //      public JsonReader reader;
                //      public string id;
                //      public int severity;
                //      public string message;
                //      public SourceSpan sourceSpan;
                //
                //       internal void <ReadDiagnostic>b__0(string propertyName)
                //       {
                //           switch (propertyName)
                //           {
                //              ....
                //               case "Message":
                //                   if (reader.Read())
                //                   {
                //                       message = (string)reader.Value;
                //                   }
                //              ...
                //           }
                //       }
                //
                //       internal string <ReadDiagnostic>b__1()
                //       {
                //           return message;
                //       }
                //  }
                //
                // And then uses that display class's "b_1" method as the lambda parameter for the diagnostic descriptor. Its reasoning is to maintain correctness
                // in the case that the above lambda mutates the value of "message". The problem with this is then the lambda has a reference to the higher level
                // display class which has the entire JsonReader payload pinned. This bloats memory in scenarios where a user has TagHelper diagnostics. Each
                // diagnostic ends up having a refernce back to the original JSON payload that created it which in our case is huge!
                return new RazorDiagnosticDescriptor(id, () => message, severity);
            }
        }

        private static SourceSpan ReadSourceSpan(JsonReader reader)
        {
            if (!reader.Read())
            {
                return SourceSpan.Undefined;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                return SourceSpan.Undefined;
            }

            var (_, filePath, absoluteIndex, lineIndex, characterIndex, length) = reader.ReadProperties(static (propertyName, arg) =>
            {
                var (reader, filePath, absoluteIndex, lineIndex, characterIndex, length) = (arg.reader, arg.filePath, arg.absoluteIndex, arg.lineIndex, arg.characterIndex, arg.length);
                switch (propertyName)
                {
                    case nameof(SourceSpan.FilePath):
                        if (reader.Read())
                        {
                            filePath = (string)reader.Value;
                        }

                        break;
                    case nameof(SourceSpan.AbsoluteIndex):
                        absoluteIndex = reader.ReadAsInt32().Value;
                        break;
                    case nameof(SourceSpan.LineIndex):
                        lineIndex = reader.ReadAsInt32().Value;
                        break;
                    case nameof(SourceSpan.CharacterIndex):
                        characterIndex = reader.ReadAsInt32().Value;
                        break;
                    case nameof(SourceSpan.Length):
                        length = reader.ReadAsInt32().Value;
                        break;
                }

                return (reader, filePath, absoluteIndex, lineIndex, characterIndex, length);
            }, (reader, filePath: (string)null, absoluteIndex: 0, lineIndex: 0, characterIndex: 0, length: 0));

            var sourceSpan = new SourceSpan(filePath, absoluteIndex, lineIndex, characterIndex, length);
            return sourceSpan;
        }

        private static void ReadToEndOfCurrentObject(JsonReader reader)
        {
            var nestingLevel = 0;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        nestingLevel++;
                        break;
                    case JsonToken.EndObject:
                        nestingLevel--;

                        if (nestingLevel == -1)
                        {
                            return;
                        }

                        break;
                }
            }

            throw new JsonSerializationException($"Could not read till end of object, end of stream. Got '{reader.TokenType}'.");
        }

        private static string Cached(string str)
        {
            if (str is null)
            {
                return null;
            }

            // Some of the strings used in TagHelperDescriptors are interned by other processes,
            // so we should avoid duplicating those.
            var interned = string.IsInterned(str);
            if (interned != null)
            {
                return interned;
            }

            // We cache all our stings here to prevent them from balooning memory in our Descriptors.
            return s_stringCache.GetOrAddValue(str);
        }
    }
}
