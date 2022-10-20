﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Common.Telemetry;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Microsoft.CodeAnalysis.Razor.Test.Common;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks
{
    public class ProjectSnapshotManagerBenchmarkBase
    {
        public ProjectSnapshotManagerBenchmarkBase()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null && !File.Exists(Path.Combine(current.FullName, "src", "Razor", "Razor.sln")))
            {
                current = current.Parent;
            }

            var root = current;
            var projectRoot = Path.Combine(root.FullName, "src", "Razor", "test", "testapps", "LargeProject");

            HostProject = new HostProject(Path.Combine(projectRoot, "LargeProject.csproj"), FallbackRazorConfiguration.MVC_2_1, rootNamespace: null);

            TextLoaders = new TextLoader[4];
            for (var i = 0; i < 4; i++)
            {
                var filePath = Path.Combine(projectRoot, "Views", "Home", $"View00{i % 4}.cshtml");
                var text = SourceText.From(filePath, encoding: null);
                TextLoaders[i] = TextLoader.From(TextAndVersion.Create(text, VersionStamp.Create()));
            }

            Documents = new HostDocument[100];
            for (var i = 0; i < Documents.Length; i++)
            {
                var filePath = Path.Combine(projectRoot, "Views", "Home", $"View00{i % 4}.cshtml");
                Documents[i] = new HostDocument(filePath, $"/Views/Home/View00{i}.cshtml", FileKinds.Legacy);
            }

            var tagHelpers = Path.Combine(root.FullName, "src", "Razor", "benchmarks", "Microsoft.AspNetCore.Razor.Microbenchmarks", "taghelpers.json");
            TagHelperResolver = new StaticTagHelperResolver(ReadTagHelpers(tagHelpers), NoOpTelemetryReporter.Instance);
        }

        internal HostProject HostProject { get; }

        internal HostDocument[] Documents { get; }

        internal TextLoader[] TextLoaders { get; }

        internal TagHelperResolver TagHelperResolver { get; }

        internal DefaultProjectSnapshotManager CreateProjectSnapshotManager()
        {
            var services = TestServices.Create(
                new IWorkspaceService[]
                {
                    TagHelperResolver,
                    new StaticProjectSnapshotProjectEngineFactory(),
                },
                Array.Empty<ILanguageService>());

            return new DefaultProjectSnapshotManager(
                new TestProjectSnapshotManagerDispatcher(),
                new TestErrorReporter(),
                Array.Empty<ProjectSnapshotChangeTrigger>(),
#pragma warning disable CA2000 // Dispose objects before losing scope
                new AdhocWorkspace(services));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        private static IReadOnlyList<TagHelperDescriptor> ReadTagHelpers(string filePath)
        {
            var serializer = new JsonSerializer();
            serializer.Converters.Add(new RazorDiagnosticJsonConverter());
            serializer.Converters.Add(TagHelperDescriptorJsonConverter.Instance);

            using (var reader = new JsonTextReader(File.OpenText(filePath)))
            {
                return serializer.Deserialize<IReadOnlyList<TagHelperDescriptor>>(reader);
            }
        }

        private class TestProjectSnapshotManagerDispatcher : ProjectSnapshotManagerDispatcher
        {
            public override bool IsDispatcherThread => true;

            public override TaskScheduler DispatcherScheduler => TaskScheduler.Default;
        }

        private class TestErrorReporter : ErrorReporter
        {
            public override void ReportError(Exception exception)
            {
            }

            public override void ReportError(Exception exception, ProjectSnapshot project)
            {
            }

            public override void ReportError(Exception exception, Project workspaceProject)
            {
            }
        }

        private class StaticTagHelperResolver : TagHelperResolver
        {
            private readonly IReadOnlyList<TagHelperDescriptor> _tagHelpers;

            public StaticTagHelperResolver(IReadOnlyList<TagHelperDescriptor> tagHelpers, ITelemetryReporter telemetryReporter)
                : base(telemetryReporter)
            {
                _tagHelpers = tagHelpers;
            }

            public override Task<TagHelperResolutionResult> GetTagHelpersAsync(Project project, ProjectSnapshot projectSnapshot, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new TagHelperResolutionResult(_tagHelpers, Array.Empty<RazorDiagnostic>()));
            }
        }

        private class StaticProjectSnapshotProjectEngineFactory : ProjectSnapshotProjectEngineFactory
        {
            public override IProjectEngineFactory FindFactory(ProjectSnapshot project)
            {
                throw new NotImplementedException();
            }

            public override IProjectEngineFactory FindSerializableFactory(ProjectSnapshot project)
            {
                throw new NotImplementedException();
            }

            public override RazorProjectEngine Create(RazorConfiguration configuration, RazorProjectFileSystem fileSystem, Action<RazorProjectEngineBuilder> configure)
            {
                return RazorProjectEngine.Create(configuration, fileSystem, b => RazorExtensions.Register(b));
            }
        }
    }
}
