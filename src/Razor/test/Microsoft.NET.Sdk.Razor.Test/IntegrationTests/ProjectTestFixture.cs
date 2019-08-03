// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.Design.IntegrationTests
{
    public class ProjectTestFixture : IAsyncLifetime
    {
        private const int MaxPackRetries = 3;
        private static readonly TimeSpan MaxPackTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);
        private readonly IMessageSink _messageSink;

        public ProjectTestFixture(IMessageSink messageSink)
        {
            _messageSink = messageSink;

            Directory.CreateDirectory(ProjectDirectory.RazorRoot);
            CopyGlobalJson();
            CopyNuGetConfig();
        }

        public Task InitializeAsync()
        {
            return PackProjectsAsync();
        }

        public Task DisposeAsync()
        {
#if !PRESERVE_WORKING_DIRECTORY
            ProjectDirectory.CleanupDirectory(ProjectDirectory.RazorRoot);
#endif

            return Task.CompletedTask;
        }

        private static void CopyGlobalJson()
        {
            var srcGlobalJson = Path.Combine(ProjectDirectory.RepositoryRoot, "global.json");
            var destinationGlobalJson = Path.Combine(ProjectDirectory.RazorRoot, "global.json");
            File.Copy(srcGlobalJson, destinationGlobalJson, overwrite: true);
        }

        private static void CopyNuGetConfig()
        {
            var source = Path.Combine(ProjectDirectory.TestAppsRoot, "NuGet.config");
            if (!File.Exists(source))
            {
                throw new InvalidOperationException("Could not find 'test/testapps/NuGet.config' file. Correct issues in current branch.");
            }

            var destination = Path.Combine(ProjectDirectory.RazorRoot, "NuGet.config");
            File.Copy(source, destination, overwrite: true);
        }

        private async Task PackProjectsAsync()
        {
            var packagePath = Path.Combine(ProjectDirectory.RazorRoot, "TestPackageRestoreSource");
            var projectsToPack = typeof(ProjectTestFixture)
                .Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(a => a.Key == "Testing.ProjectToPack")
                .Select(a => a.Value)
                .ToArray();

            foreach (var project in projectsToPack)
            {
                _messageSink.OnMessage(new DiagnosticMessage($"Packing project '{project}'."));

                var psi = new ProcessStartInfo
                {
                    FileName = DotNetMuxer.MuxerPathOrDefault(),
#if DEBUG
                    Arguments = $"msbuild /t:Restore;Pack /p:Configuration=Debug /p:PackageOutputPath=\"{packagePath}\"",
#else
                    Arguments = $"msbuild /t:Restore;Pack /p:Configuration=Release /p:PackageOutputPath=\"{packagePath}\"",
#endif
                    WorkingDirectory = project,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                for (var i = 0; i < MaxPackRetries; i++)
                {
                    try
                    {
                        var result = await MSBuildProcessManager.RunProcessCoreAsync(psi, MaxPackTimeout);

                        _messageSink.OnMessage(new DiagnosticMessage(result.Output));
                        Xunit.Assert.Equal(0, result.ExitCode);
                        break;
                    }
                    catch
                    {
                        await Task.Delay(RetryDelay);
                    }
                }
            }
        }
    }
}
