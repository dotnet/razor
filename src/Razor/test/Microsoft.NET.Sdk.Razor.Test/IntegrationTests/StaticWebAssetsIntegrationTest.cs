﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.CommandLineUtils;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.Design.IntegrationTests
{
    public class StaticWebAssetsIntegrationTest : MSBuildIntegrationTestBase, IClassFixture<BuildServerTestFixture>, IClassFixture<PackageTestProjectsFixture>, IAsyncLifetime
    {
        public StaticWebAssetsIntegrationTest(
            BuildServerTestFixture buildServer,
            PackageTestProjectsFixture packageTestProjects,
            ITestOutputHelper output)
            : base(buildServer)
        {
            UseLocalPackageCache = true;
            PackageTestProjects = packageTestProjects;
            Output = output;
        }

        public PackageTestProjectsFixture PackageTestProjects { get; private set; }

        public ITestOutputHelper Output { get; private set; }

        [Fact(Skip = "https://github.com/aspnet/AspNetCore/issues/17233")]
        [InitializeTestProject("AppWithPackageAndP2PReference",language: "C#", additionalProjects: new[] { "ClassLibrary", "ClassLibrary2" })]
        public async Task Build_GeneratesStaticWebAssetsManifest_Success_CreatesManifest()
        {
            var result = await DotnetMSBuild("Build", "/restore");

            var expectedManifest = GetExpectedManifest();

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache");
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Skip this check on mac as the CI seems to use a somewhat different path on OSX.
                // This check works just fine on a local OSX instance, but the CI path seems to require prepending /private.
                // There is nothing OS specific about publishing this file, so the chances of this breaking are infinitesimal.
                Assert.FileExists(result, OutputPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml");
            }

            var path = Assert.FileExists(result, OutputPath, "AppWithPackageAndP2PReference.dll");
            var manifest = Assert.FileExists(result, OutputPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml");
            var data = File.ReadAllText(manifest);
            Assert.Equal(expectedManifest, data);
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference", additionalProjects: new[] { "ClassLibrary", "ClassLibrary2" })]
        public async Task Publish_CopiesStaticWebAssetsToDestinationFolder()
        {
            var result = await DotnetMSBuild("Publish", "/restore");

            Assert.BuildPassed(result);

            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js"));
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.v4.js"));
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary2", "css", "site.css"));
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary2", "js", "project-direct-dep.js"));
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css"));
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js"));
            Assert.FileExists(result, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js"));

            // Validate that static web assets don't get published as content too on their regular path
            Assert.FileDoesNotExist(result, PublishOutputPath, Path.Combine("wwwroot", "js", "project-transitive-dep.js"));
            Assert.FileDoesNotExist(result, PublishOutputPath, Path.Combine("wwwroot", "js", "project-transitive-dep.v4.js"));

            // Validate that the manifest never gets copied
            Assert.FileDoesNotExist(result, PublishOutputPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml");
        }

        [ConditionalFact(Skip = "https://github.com/dotnet/aspnetcore/issues/18543")]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [InitializeTestProject("AppWithPackageAndP2PReferenceAndRID", additionalProjects: new[] { "ClassLibrary", "ClassLibrary2" })]
        public async Task Publish_CopiesStaticWebAssetsToDestinationFolder_PublishSingleFile()
        {
            var result = await DotnetMSBuild("Publish", $"/restore /p:PublishSingleFile=true /p:ReferenceLocallyBuiltPackages=true");

            Assert.BuildPassed(result);
            var publishOutputPath = GetRidSpecificPublishOutputPath("win-x64");
            Assert.FileExists(result, publishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js"));
            Assert.FileExists(result, publishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.v4.js"));
            Assert.FileExists(result, publishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary2", "css", "site.css"));
            Assert.FileExists(result, publishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary2", "js", "project-direct-dep.js"));
            Assert.FileExists(result, publishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css"));
            Assert.FileExists(result, publishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js"));
            Assert.FileExists(result, publishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js"));

            // Validate that static web assets don't get published as content too on their regular path
            Assert.FileDoesNotExist(result, publishOutputPath, Path.Combine("wwwroot", "js", "project-transitive-dep.js"));
            Assert.FileDoesNotExist(result, publishOutputPath, Path.Combine("wwwroot", "js", "project-transitive-dep.v4.js"));

            // Validate that the manifest never gets copied
            Assert.FileDoesNotExist(result, publishOutputPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml");
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference", additionalProjects: new[] { "ClassLibrary", "ClassLibrary2" })]
        public async Task Publish_WithBuildReferencesDisabled_CopiesStaticWebAssetsToDestinationFolder()
        {
            var build = await DotnetMSBuild("Build", "/restore", suppressTimeout: true);

            Assert.BuildPassed(build);

            var publish = await DotnetMSBuild("Publish", "/p:BuildProjectReferences=false;PublishIISAssets=true");

            Assert.BuildPassed(publish);

            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.v4.js"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary2", "css", "site.css"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary2", "js", "project-direct-dep.js"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js"));
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/18543")]
        [InitializeTestProject("AppWithPackageAndP2PReference", additionalProjects: new[] { "ClassLibrary", "ClassLibrary2" })]
        public async Task Publish_NoBuild_CopiesStaticWebAssetsToDestinationFolder()
        {
            var build = await DotnetMSBuild("Build", "/restore");

            Assert.BuildPassed(build);

            var publish = await DotnetMSBuild("Publish", "/p:NoBuild=true");

            Assert.BuildPassed(publish);

            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.v4.js"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary2", "css", "site.css"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "ClassLibrary2", "js", "project-direct-dep.js"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js"));
            Assert.FileExists(publish, PublishOutputPath, Path.Combine("wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js"));
        }

        [Fact]
        [InitializeTestProject("SimpleMvc")]
        public async Task Build_DoesNotEmbedManifestWhen_NoStaticResourcesAvailable()
        {
            var result = await DotnetMSBuild("Build", "/restore");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "staticwebassets", "SimpleMvc.StaticWebAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "staticwebassets", "SimpleMvc.StaticWebAssets.Manifest.cache");
            Assert.FileDoesNotExist(result, OutputPath, "SimpleMvc.StaticWebAssets.xml");

            var path = Assert.FileExists(result, OutputPath, "SimpleMvc.dll");
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference",language: "C#", additionalProjects: new[] { "ClassLibrary", "ClassLibrary2" })]
        public async Task Clean_Success_RemovesManifestAndCache()
        {
            var result = await DotnetMSBuild("Build", "/restore");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache");

            var cleanResult = await DotnetMSBuild("Clean");

            Assert.BuildPassed(cleanResult);

            // Clean should delete the manifest and the cache.
            Assert.FileDoesNotExist(result, IntermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache");
            Assert.FileDoesNotExist(result, IntermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.xml");
        }

        [Fact(Skip = "https://github.com/aspnet/AspNetCore/issues/17233")]
        [InitializeTestProject("AppWithPackageAndP2PReference",language: "C#", additionalProjects: new[] { "ClassLibrary", "ClassLibrary2" })]
        public async Task Rebuild_Success_RecreatesManifestAndCache()
        {
            // Arrange
            var result = await DotnetMSBuild("Build", "/restore");

            var expectedManifest = GetExpectedManifest();

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache");

            var directoryPath = Path.Combine(result.Project.DirectoryPath, IntermediateOutputPath, "staticwebassets");
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml"),
                Path.Combine(directoryPath, "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            var rebuild = await DotnetMSBuild("Rebuild");

            // Assert
            Assert.BuildPassed(rebuild);

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                Assert.NotEqual(thumbPrints[file], thumbprint);
            }

            var path = Assert.FileExists(result, OutputPath, "AppWithPackageAndP2PReference.dll");
            var manifest = Assert.FileExists(result, OutputPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml");
            var data = File.ReadAllText(manifest);
            Assert.Equal(expectedManifest, data);
        }

        [Fact]
        [InitializeTestProject("AppWithPackageAndP2PReference",language: "C#", additionalProjects: new[] { "ClassLibrary", "ClassLibrary2" })]
        public async Task GenerateStaticWebAssetsManifest_IncrementalBuild_ReusesManifest()
        {
            var result = await DotnetMSBuild("GenerateStaticWebAssetsManifest", "/restore");

            Assert.BuildPassed(result);

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            Assert.FileExists(result, IntermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.xml");
            Assert.FileExists(result, IntermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache");

            var directoryPath = Path.Combine(result.Project.DirectoryPath, IntermediateOutputPath, "staticwebassets");
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml"),
                Path.Combine(directoryPath, "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            var incremental = await DotnetMSBuild("GenerateStaticWebAssetsManifest");

            // Assert
            Assert.BuildPassed(incremental);

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = GetThumbPrint(file);
                Assert.Equal(thumbPrints[file], thumbprint);
            }
        }

        public Task InitializeAsync()
        {
            return PackageTestProjects.PackAsync(Output);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        private string GetExpectedManifest()
        {
            // We need to do this for Mac as apparently the temp folder in mac is prepended by /private by the os, even though the current user
            // can refer to it without the /private prefix. We don't care a lot about the specific path in this test as we will have tests that
            // validate the behavior at runtime.
            var source = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"/private{Project.SolutionPath}" : Project.SolutionPath;

            var restorePath = LocalNugetPackagesCacheTempPath;
            var projects = new[]
            {
                Path.Combine(restorePath, "packagelibrarytransitivedependency", "1.0.0", "build", "..", "staticwebassets") + Path.DirectorySeparatorChar,
                Path.Combine(restorePath, "packagelibrarydirectdependency", "1.0.0", "build", "..", "staticwebassets") + Path.DirectorySeparatorChar,
                Path.GetFullPath(Path.Combine(source, "ClassLibrary", "wwwroot")) + Path.DirectorySeparatorChar,
                Path.GetFullPath(Path.Combine(source, "ClassLibrary2", "wwwroot")) + Path.DirectorySeparatorChar
            };

            return $@"<StaticWebAssets Version=""1.0"">
  <ContentRoot BasePath=""_content/ClassLibrary"" Path=""{projects[2]}"" />
  <ContentRoot BasePath=""_content/ClassLibrary2"" Path=""{projects[3]}"" />
  <ContentRoot BasePath=""_content/PackageLibraryDirectDependency"" Path=""{projects[1]}"" />
  <ContentRoot BasePath=""_content/PackageLibraryTransitiveDependency"" Path=""{projects[0]}"" />
</StaticWebAssets>";
        }
    }

    public class PackageTestProjectsFixture
    {
        private const int MaxPackRetries = 3;
        private const int MaxPackTimeoutInMinutes = 5;

        private bool _packed;

        internal async Task PackAsync(ITestOutputHelper output)
        {
            if (_packed)
            {
                return;
            }

            var projectsToPack = GetProjectsToPack();

            foreach (var project in projectsToPack)
            {
                output.WriteLine(project);
            }

            foreach (var project in projectsToPack)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = DotNetMuxer.MuxerPathOrDefault(),
#if DEBUG
                    Arguments = "msbuild /t:Restore;Pack /p:Configuration=Debug",
#else
                    Arguments = "msbuild /t:Restore;Pack /p:Configuration=Release",
#endif
                    WorkingDirectory = project,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                for (int i = 0; i < MaxPackRetries; i++)
                {
                    try
                    {
                        var result = await MSBuildProcessManager.RunProcessCoreAsync(
                            psi,
                            TimeSpan.FromMinutes(MaxPackTimeoutInMinutes));

                        output.WriteLine(result.Output);
                        Assert.Equal(0, result.ExitCode);
                        break;
                    }
                    catch
                    {
                        await Task.Delay(1000);
                    }
                }
            }

            _packed = true;
        }

        public static string[] GetProjectsToPack()
        {
            return typeof(PackageTestProjectsFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(a => a.Key == "Testing.ProjectToPack")
                .Select(a => a.Value)
                .ToArray();
        }
    }
}
