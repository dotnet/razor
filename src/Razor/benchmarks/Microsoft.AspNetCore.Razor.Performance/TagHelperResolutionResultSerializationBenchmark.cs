// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Razor.Performance
{
    public class TagHelperResolutionResultSerializationBenchmark
    {
        private readonly byte[] _tagHelperBuffer;

        public TagHelperResolutionResultSerializationBenchmark()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null && !File.Exists(Path.Combine(current.FullName, "taghelpers.json")))
            {
                current = current.Parent;
            }

            var tagHelperFilePath = Path.Combine(current.FullName, "taghelpers.json");
            _tagHelperBuffer = File.ReadAllBytes(tagHelperFilePath);

            // Deserialize from json file.
            OriginalSerializer = new JsonSerializer();
            OriginalSerializer.Converters.Add(new TagHelperDescriptorJsonConverter());

            EnhancedSerializer = new JsonSerializer();
            EnhancedSerializer.Converters.Add(new TagHelperDescriptorJsonConverter());
            EnhancedSerializer.Converters.Add(new TagHelperResolutionResultJsonConverter());
            using (var stream = new MemoryStream(_tagHelperBuffer))
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                var tagHelpers = EnhancedSerializer.Deserialize<IReadOnlyList<TagHelperDescriptor>>(reader);
                TagHelperResolutionResult = new TagHelperResolutionResult(tagHelpers, Array.Empty<RazorDiagnostic>());
            }
        }

        public JsonSerializer OriginalSerializer { get; set; }
        public JsonSerializer EnhancedSerializer { get; set; }
        private TagHelperResolutionResult TagHelperResolutionResult { get; }

        [Benchmark(Description = "Razor TagHelperResolutionResult Roundtrip JObject Serialization")]
        public void TagHelper_JObject_Serialization_RoundTrip()
        {
            var jobject = JObject.FromObject(TagHelperResolutionResult, OriginalSerializer);

            MemoryStream originalStream;
            using (originalStream = new MemoryStream())
            using (var writer = new StreamWriter(originalStream, Encoding.UTF8, bufferSize: 4096))
            {
                OriginalSerializer.Serialize(writer, jobject);
            }

            JObject deserializedResult;
            var stream = new MemoryStream(originalStream.GetBuffer());
            using (stream)
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                deserializedResult = OriginalSerializer.Deserialize<JObject>(reader);
            }

            var result = deserializedResult.ToObject<TagHelperResolutionResult>(OriginalSerializer);
        }

        [Benchmark(Description = "Razor TagHelperResolutionResult Roundtrip JsonConverter Serialization")]
        public void TagHelper_JsonConvert_Serialization_RoundTrip()
        {
            MemoryStream originalStream;
            using (originalStream = new MemoryStream())
            using (var writer = new StreamWriter(originalStream, Encoding.UTF8, bufferSize: 4096))
            {
                EnhancedSerializer.Serialize(writer, TagHelperResolutionResult);
            }

            TagHelperResolutionResult deserializedResult;
            var stream = new MemoryStream(originalStream.GetBuffer());
            using (stream)
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                deserializedResult = EnhancedSerializer.Deserialize<TagHelperResolutionResult>(reader);
            }
        }
    }
}
