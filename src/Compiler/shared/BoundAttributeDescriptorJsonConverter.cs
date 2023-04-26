//// Licensed to the .NET Foundation under one or more agreements.
//// The .NET Foundation licenses this file to you under the MIT license.

//#nullable disable

//using System;
//using System.Collections.Generic;
//using Microsoft.AspNetCore.Razor.Language;
//using Newtonsoft.Json;

//namespace Microsoft.CodeAnalysis.Razor.Serialization;

//internal class BoundAttributeDescriptorJsonConverter : JsonConverter
//{
//    public override bool CanConvert(Type objectType)
//    {
//        return typeof(BoundAttributeDescriptor).IsAssignableFrom(objectType);
//    }

//    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//    {
//        BoundAttributeDescriptorBuilder builder = new DefaultBoundAttributeDescriptorBuilder();
//        R
//    }

//    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//    {
//    }

//    public static void ReadBoundAttribute(JsonReader reader, BoundAttributeDescriptorBuilder attribute)
//    {
//        //reader.ReadProperties(propertyName =>
//        {
//            switch (propertyName)
//            {
//                case nameof(BoundAttributeDescriptor.Name):
//                    if (reader.Read())
//                    {
//                        var name = (string)reader.Value;
//                        attribute.Name = name;
//                    }
//                    break;
//                case nameof(BoundAttributeDescriptor.TypeName):
//                    if (reader.Read())
//                    {
//                        var typeName = (string)reader.Value;
//                        attribute.TypeName = typeName;
//                    }
//                    break;
//                case nameof(BoundAttributeDescriptor.Documentation):
//                    if (reader.Read())
//                    {
//                        var documentation = (string)reader.Value;
//                        attribute.Documentation = documentation;
//                    }
//                    break;
//                case nameof(BoundAttributeDescriptor.IndexerNamePrefix):
//                    if (reader.Read())
//                    {
//                        var indexerNamePrefix = (string)reader.Value;
//                        if (indexerNamePrefix != null)
//                        {
//                            attribute.IsDictionary = true;
//                            attribute.IndexerAttributeNamePrefix = indexerNamePrefix;
//                        }
//                    }
//                    break;
//                case nameof(BoundAttributeDescriptor.IndexerTypeName):
//                    if (reader.Read())
//                    {
//                        var indexerTypeName = (string)reader.Value;
//                        if (indexerTypeName != null)
//                        {
//                            attribute.IsDictionary = true;
//                            attribute.IndexerValueTypeName = indexerTypeName;
//                        }
//                    }
//                    break;
//                case nameof(BoundAttributeDescriptor.IsEnum):
//                    if (reader.Read())
//                    {
//                        var isEnum = (bool)reader.Value;
//                        attribute.IsEnum = isEnum;
//                    }
//                    break;
//                case nameof(BoundAttributeDescriptor.IsEditorRequired):
//                    if (reader.Read())
//                    {
//                        var value = (bool)reader.Value;
//                        attribute.IsEditorRequired = value;
//                    }
//                    break;
//                case nameof(BoundAttributeDescriptor.BoundAttributeParameters):
//                    ReadBoundAttributeParameters(reader, attribute);
//                    break;
//                case nameof(BoundAttributeDescriptor.Diagnostics):
//                    TagHelperDescriptorJsonConverter.ReadDiagnostics(reader, attribute.Diagnostics);
//                    break;
//                case nameof(BoundAttributeDescriptor.Metadata):
//                    TagHelperDescriptorJsonConverter.ReadMetadata(reader, attribute.Metadata);
//                    break;
//            }
//        });
//    }

//    private static void ReadBoundAttributeParameters(JsonReader reader, BoundAttributeDescriptorBuilder builder)
//    {
//        if (!reader.Read())
//        {
//            return;
//        }

//        if (reader.TokenType != JsonToken.StartArray)
//        {
//            return;
//        }

//        do
//        {
//            ReadBoundAttributeParameter(reader, builder);
//        } while (reader.TokenType != JsonToken.EndArray);
//    }

//    private static void ReadBoundAttributeParameter(JsonReader reader, BoundAttributeDescriptorBuilder builder)
//    {
//        if (!reader.Read())
//        {
//            return;
//        }

//        if (reader.TokenType != JsonToken.StartObject)
//        {
//            return;
//        }

//        builder.BindAttributeParameter(parameter =>
//        {
//            reader.ReadProperties(propertyName =>
//            {
//                switch (propertyName)
//                {
//                    case nameof(BoundAttributeParameterDescriptor.Name):
//                        if (reader.Read())
//                        {
//                            var name = (string)reader.Value;
//                            parameter.Name = name;
//                        }
//                        break;
//                    case nameof(BoundAttributeParameterDescriptor.TypeName):
//                        if (reader.Read())
//                        {
//                            var typeName = (string)reader.Value;
//                            parameter.TypeName = typeName;
//                        }
//                        break;
//                    case nameof(BoundAttributeParameterDescriptor.IsEnum):
//                        if (reader.Read())
//                        {
//                            var isEnum = (bool)reader.Value;
//                            parameter.IsEnum = isEnum;
//                        }
//                        break;
//                    case nameof(BoundAttributeParameterDescriptor.Documentation):
//                        if (reader.Read())
//                        {
//                            var documentation = (string)reader.Value;
//                            parameter.Documentation = documentation;
//                        }
//                        break;
//                    case nameof(BoundAttributeParameterDescriptor.Metadata):
//                        TagHelperDescriptorJsonConverter.ReadMetadata(reader, parameter.Metadata);
//                        break;
//                    case nameof(BoundAttributeParameterDescriptor.Diagnostics):
//                        TagHelperDescriptorJsonConverter.ReadDiagnostics(reader, parameter.Diagnostics);
//                        break;
//                }
//            });
//        });
//    }
//}
