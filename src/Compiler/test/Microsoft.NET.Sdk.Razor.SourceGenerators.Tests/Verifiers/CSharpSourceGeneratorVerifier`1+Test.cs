// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

// Uncomment the following line to write expected files to disk
////#define WRITE_EXPECTED

#if WRITE_EXPECTED
#warning WRITE_EXPECTED is fine for local builds, but should not be merged to the main branch.
#endif

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators.Verifiers
{
    public static partial class CSharpSourceGeneratorVerifier<TSourceGenerator>
        where TSourceGenerator : IIncrementalGenerator, new()
    {
        public class Test : CSharpSourceGeneratorTest<EmptySourceGeneratorProvider, XUnitVerifier>
        {
            private readonly string? _testFile;
            private readonly string? _testMethod;

            public Test([CallerFilePath] string? testFile = null, [CallerMemberName] string? testMethod = null)
            {
                CompilerDiagnostics = CompilerDiagnostics.Warnings;

                _testFile = testFile;
                _testMethod = testMethod;

#if WRITE_EXPECTED
                TestBehaviors |= TestBehaviors.SkipGeneratedSourcesCheck;
#endif
            }

            public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.Default;

            protected override IEnumerable<Type> GetSourceGenerators()
            {
                yield return typeof(TSourceGenerator);
            }

            protected override CompilationOptions CreateCompilationOptions()
            {
                var compilationOptions = (CSharpCompilationOptions)base.CreateCompilationOptions();
                return compilationOptions
                    .WithAllowUnsafe(false)
                    .WithWarningLevel(99)
                    .WithSpecificDiagnosticOptions(compilationOptions.SpecificDiagnosticOptions.SetItem("CS8019", ReportDiagnostic.Suppress));
            }

            protected override ParseOptions CreateParseOptions()
            {
                return ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion);
            }

            protected override async Task<(Compilation compilation, ImmutableArray<Diagnostic> generatorDiagnostics)> GetProjectCompilationAsync(Project project, IVerifier verifier, CancellationToken cancellationToken)
            {
                var resourceDirectory = Path.Combine(Path.GetDirectoryName(_testFile)!, "Resources", _testMethod!);

                var (compilation, generatorDiagnostics) = await base.GetProjectCompilationAsync(project, verifier, cancellationToken);
                var expectedNames = new HashSet<string>();
                foreach (var tree in compilation.SyntaxTrees.Skip(project.DocumentIds.Count))
                {
                    WriteTreeToDiskIfNecessary(tree, resourceDirectory);
                    expectedNames.Add(Path.GetFileName(tree.FilePath));
                }

                var currentTestPrefix = $"{typeof(RazorSourceGeneratorTests).Assembly.GetName().Name}.Resources.{_testMethod}.";
                foreach (var name in GetType().Assembly.GetManifestResourceNames())
                {
                    if (!name.StartsWith(currentTestPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!expectedNames.Contains(name[currentTestPrefix.Length..]))
                    {
                        throw new InvalidOperationException($"Unexpected test resource: {name[currentTestPrefix.Length..]}");
                    }
                }

                return (compilation, generatorDiagnostics);
            }

            public Test AddMetadata()
            {
                var globalConfig = new StringBuilder(@"is_global = true

build_property.RazorConfiguration = Default
build_property.RootNamespace = MyApp
build_property.RazorLangVersion = Latest
build_property.GenerateRazorMetadataSourceChecksumAttributes = false
");

                foreach (var (filename, _) in TestState.AdditionalFiles)
                {
                    globalConfig.AppendLine(CultureInfo.InvariantCulture, $@"[{filename}]
build_metadata.AdditionalFiles.TargetPath = {Convert.ToBase64String(Encoding.UTF8.GetBytes(getRelativeFilePath(filename)))}");
                }

                TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig.ToString()));

                return this;

                static string getRelativeFilePath(string absolutePath)
                {
                    if (absolutePath.StartsWith("/0/", StringComparison.Ordinal))
                    {
                        return absolutePath["/0/".Length..];
                    }
                    else if (absolutePath.StartsWith("/", StringComparison.Ordinal))
                    {
                        return absolutePath["/".Length..];
                    }
                    else
                    {
                        return absolutePath;
                    }
                }
            }

            /// <summary>
            /// Loads expected generated sources from embedded resources based on the test name.
            /// </summary>
            /// <param name="testMethod">The current test method name.</param>
            /// <returns>The current <see cref="Test"/> instance.</returns>
            public Test AddGeneratedSources([CallerMemberName] string? testMethod = null)
            {
                var expectedPrefix = $"{typeof(RazorSourceGeneratorTests).Assembly.GetName().Name}.Resources.{testMethod}.";
                foreach (var resourceName in typeof(Test).Assembly.GetManifestResourceNames())
                {
                    if (!resourceName.StartsWith(expectedPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    using var resourceStream = typeof(RazorSourceGeneratorTests).Assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException();
                    using var reader = new StreamReader(resourceStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
                    var name = resourceName[expectedPrefix.Length..];
                    TestState.GeneratedSources.Add((typeof(RazorSourceGenerator), name, reader.ReadToEnd()));
                }

                // An error will be reported if there are no sources or generated sources in the compilation. To bypass
                // during the initial test construction, we add a default empty generated source knowing that it will
                // not be validated.
                if (TestBehaviors.HasFlag(TestBehaviors.SkipGeneratedSourcesCheck) && !TestState.Sources.Any() && !TestState.GeneratedSources.Any())
                {
                    TestState.GeneratedSources.Add(("/ignored_file", ""));
                }

                return this;
            }

            [Conditional("WRITE_EXPECTED")]
            private static void WriteTreeToDiskIfNecessary(SyntaxTree tree, string resourceDirectory)
            {
                if (tree.Encoding is null)
                {
                    throw new ArgumentException("Syntax tree encoding was not specified");
                }

                var name = Path.GetFileName(tree.FilePath);
                var filePath = Path.Combine(resourceDirectory, name);
                Directory.CreateDirectory(resourceDirectory);
                File.WriteAllText(filePath, tree.GetText().ToString(), tree.Encoding);
            }
        }
    }
}
