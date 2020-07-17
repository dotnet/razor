// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Razor.Performance
{
    public class FullProjectSnapshotHandleSerializationBenchmark
    {
        private static readonly string ExpectedFilePath = "C:\\Users\\admin\\location\\blazorserver\\blazorserver.csproj";
        private static readonly int ExpectedTagHelperCount = 228;

        private JsonSerializer Serializer { get; set; }
        private JsonReader Reader { get; set; }
        private byte[] FullProjectSnapshotBuffer { get; set; }

        [IterationSetup]
        public void Setup()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null && !File.Exists(Path.Combine(current.FullName, "ProjectSystem", "project.razor.json")))
            {
                current = current.Parent;
            }

            var fullProjectSnapshotFilePath = Path.Combine(current.FullName, "ProjectSystem", "project.razor.json");
            FullProjectSnapshotBuffer = File.ReadAllBytes(fullProjectSnapshotFilePath);

            Serializer = new JsonSerializer();
            Serializer.Converters.RegisterRazorConverters();
            Serializer.Converters.Add(FullProjectSnapshotHandleJsonConverter.Instance);
        }

        [Benchmark(Description = "Razor FullProjectSnapshotHandle Roundtrip JObject Serialization")]
        public void TagHelper_JObject_Serialization_RoundTrip()
        {
            var stream = new MemoryStream(FullProjectSnapshotBuffer);
            Reader = new JsonTextReader(new StreamReader(stream));

            var obj = JObject.Load(Reader);

            // We need to add a serialization format to the project response to indicate that this version of the code is compatible with what's being serialized.
            // This scenario typically happens when a user has an incompatible serialized project snapshot but is using the latest Razor bits.

            obj.TryGetValue("SerializationFormat", out var serializationFormatToken);
            var serializationFormat = serializationFormatToken.Value<string>();

            var filePath = obj[nameof(FullProjectSnapshotHandle.FilePath)].Value<string>();
            var configuration = obj[nameof(FullProjectSnapshotHandle.Configuration)].ToObject<RazorConfiguration>(Serializer);
            var rootNamespace = obj[nameof(FullProjectSnapshotHandle.RootNamespace)].ToObject<string>(Serializer);
            var projectWorkspaceState = obj[nameof(FullProjectSnapshotHandle.ProjectWorkspaceState)].ToObject<ProjectWorkspaceState>(Serializer);
            var documents = obj[nameof(FullProjectSnapshotHandle.Documents)].ToObject<DocumentSnapshotHandle[]>(Serializer);

            var res = new FullProjectSnapshotHandle(filePath, configuration, rootNamespace, projectWorkspaceState, documents);

            if (res.FilePath != ExpectedFilePath ||
                res.ProjectWorkspaceState.TagHelpers.Count != ExpectedTagHelperCount)
            {
                throw new InvalidDataException();
            }
        }

        [Benchmark(Description = "Razor ProjectSnapshotHandle Roundtrip JsonConverter Serialization")]
        public void TagHelper_JsonConvert_Serialization_RoundTrip()
        {
            var stream = new MemoryStream(FullProjectSnapshotBuffer);
            Reader = new JsonTextReader(new StreamReader(stream));

            Reader.Read();

            var res = FullProjectSnapshotHandleJsonConverter.Instance.ReadJson(Reader, typeof(FullProjectSnapshotHandle), null, Serializer) as FullProjectSnapshotHandle;

            if (res.FilePath != ExpectedFilePath ||
                res.ProjectWorkspaceState.TagHelpers.Count != ExpectedTagHelperCount)
            {
                throw new InvalidDataException();
            }
        }
    }
}
