// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

public abstract class RazorSourceGeneratorTestsBase
{
    private static readonly Project _baseProject = CreateBaseProject();

    protected static async ValueTask<GeneratorDriver> GetDriverAsync(Project project)
    {
        var (driver, _) = await GetDriverWithAdditionalTextAsync(project);
        return driver;
    }

    protected static async ValueTask<(GeneratorDriver, ImmutableArray<AdditionalText>)> GetDriverWithAdditionalTextAsync(Project project, Action<TestAnalyzerConfigOptionsProvider>? configureGlobalOptions = null)
    {
        var result = await GetDriverWithAdditionalTextAndProviderAsync(project, configureGlobalOptions);
        return (result.Item1, result.Item2);
    }

    protected static async ValueTask<(GeneratorDriver, ImmutableArray<AdditionalText>, TestAnalyzerConfigOptionsProvider)> GetDriverWithAdditionalTextAndProviderAsync(Project project, Action<TestAnalyzerConfigOptionsProvider>? configureGlobalOptions = null)
    {
        var razorSourceGenerator = new RazorSourceGenerator().AsSourceGenerator();
        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(new[] { razorSourceGenerator }, parseOptions: (CSharpParseOptions)project.ParseOptions!, driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));

        var optionsProvider = new TestAnalyzerConfigOptionsProvider();
        optionsProvider.TestGlobalOptions["build_property.RazorConfiguration"] = "Default";
        optionsProvider.TestGlobalOptions["build_property.RootNamespace"] = "MyApp";
        optionsProvider.TestGlobalOptions["build_property.RazorLangVersion"] = "Latest";
        optionsProvider.TestGlobalOptions["build_property.GenerateRazorMetadataSourceChecksumAttributes"] = "false";

        var additionalTexts = ImmutableArray<AdditionalText>.Empty;

        foreach (var document in project.AdditionalDocuments)
        {
            var additionalText = new TestAdditionalText(document.Name, await document.GetTextAsync());
            additionalTexts = additionalTexts.Add(additionalText);

            var additionalTextOptions = new TestAnalyzerConfigOptions
            {
                ["build_metadata.AdditionalFiles.TargetPath"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(additionalText.Path)),
            };

            optionsProvider.AdditionalTextOptions[additionalText.Path] = additionalTextOptions;
        }

        configureGlobalOptions?.Invoke(optionsProvider);

        driver = driver
            .AddAdditionalTexts(additionalTexts)
            .WithUpdatedAnalyzerConfigOptions(optionsProvider);

        return (driver, additionalTexts, optionsProvider);
    }

    protected static GeneratorRunResult RunGenerator(Compilation compilation, ref GeneratorDriver driver, params Action<Diagnostic>[] expectedDiagnostics)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out compilation, out _);

        var actualDiagnostics = compilation.GetDiagnostics().Where(d => d.Severity != DiagnosticSeverity.Hidden);
        Assert.Collection(actualDiagnostics, expectedDiagnostics);

        var result = driver.GetRunResult();
        return result.Results.Single();
    }

    protected static Project CreateTestProject(
        Dictionary<string, string> additonalSources,
        Dictionary<string, string>? sources = null)
    {
        var project = _baseProject;

        if (sources is not null)
        {
            foreach (var (name, source) in sources)
            {
                project = project.AddDocument(name, source).Project;
            }
        }

        foreach (var (name, source) in additonalSources)
        {
            project = project.AddAdditionalDocument(name, source).Project;
        }

        return project;
    }

    private class AppLocalResolver : ICompilationAssemblyResolver
    {
        public bool TryResolveAssemblyPaths(CompilationLibrary library, List<string>? assemblies)
        {
            foreach (var assembly in library.Assemblies)
            {
                var dll = Path.Combine(Directory.GetCurrentDirectory(), "refs", Path.GetFileName(assembly));
                if (File.Exists(dll))
                {
                    assemblies!.Add(dll);
                    return true;
                }

                dll = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(assembly));
                if (File.Exists(dll))
                {
                    assemblies!.Add(dll);
                    return true;
                }
            }

            return false;
        }
    }

    private static Project CreateBaseProject()
    {
        var projectId = ProjectId.CreateNewId(debugName: "TestProject");

        var solution = new AdhocWorkspace()
           .CurrentSolution
           .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp);

        var project = solution.Projects.Single()
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable));

        project = project.WithParseOptions(((CSharpParseOptions)project.ParseOptions!).WithLanguageVersion(LanguageVersion.Preview));


        foreach (var defaultCompileLibrary in DependencyContext.Load(typeof(RazorSourceGeneratorTests).Assembly)!.CompileLibraries)
        {
            foreach (var resolveReferencePath in defaultCompileLibrary.ResolveReferencePaths(new AppLocalResolver()))
            {
                project = project.AddMetadataReference(MetadataReference.CreateFromFile(resolveReferencePath));
            }
        }

        // The deps file in the project is incorrect and does not contain "compile" nodes for some references.
        // However these binaries are always present in the bin output. As a "temporary" workaround, we'll add
        // every dll file that's present in the test's build output as a metadatareference.
        foreach (var assembly in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            if (!project.MetadataReferences.Any(c => string.Equals(Path.GetFileNameWithoutExtension(c.Display), Path.GetFileNameWithoutExtension(assembly), StringComparison.OrdinalIgnoreCase)))
            {
                project = project.AddMetadataReference(MetadataReference.CreateFromFile(assembly));
            }
        }

        return project;
    }

    protected class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => TestGlobalOptions;

        public TestAnalyzerConfigOptions TestGlobalOptions { get; } = new TestAnalyzerConfigOptions();

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => throw new NotImplementedException();

        public Dictionary<string, TestAnalyzerConfigOptions> AdditionalTextOptions { get; } = new();

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return AdditionalTextOptions.TryGetValue(textFile.Path, out var options) ? options : new TestAnalyzerConfigOptions();
        }

        public TestAnalyzerConfigOptionsProvider Clone()
        {
            var provider = new TestAnalyzerConfigOptionsProvider();
            foreach (var option in this.TestGlobalOptions.Options)
            {
                provider.TestGlobalOptions[option.Key] = option.Value;
            }
            foreach (var option in this.AdditionalTextOptions)
            {
                TestAnalyzerConfigOptions newOptions = new TestAnalyzerConfigOptions();
                foreach (var subOption in option.Value.Options)
                {
                    newOptions[subOption.Key] = subOption.Value;
                }
                provider.AdditionalTextOptions[option.Key] = newOptions;

            }
            return provider;
        }
    }

    protected class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        public Dictionary<string, string> Options { get; } = new();

        public string this[string name]
        {
            get => Options[name];
            set => Options[name] = value;
        }

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            => Options.TryGetValue(key, out value);
    }
}

internal static class Extensions
{
    // UTF-8 with BOM
    private static readonly Encoding _baselineEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static GeneratorRunResult VerifyPageOutput(this GeneratorRunResult result, params string[] expectedOutput)
    {
        if (expectedOutput.Length == 1 && string.IsNullOrWhiteSpace(expectedOutput[0]))
        {
            Assert.True(false, GenerateExpectedOutput(result));
        }
        else
        {
            Assert.Equal(expectedOutput.Length, result.GeneratedSources.Length);
            for (int i = 0; i < result.GeneratedSources.Length; i++)
            {
                var text = TrimChecksum(result.GeneratedSources[i].SourceText.ToString());
                Assert.Equal(text, TrimChecksum(expectedOutput[i]), ignoreWhiteSpaceDifferences: true);
            }
        }

        return result;
    }

    public static GeneratorRunResult VerifyOutputsMatchBaseline(this GeneratorRunResult result,
        [CallerFilePath] string testPath = null!, [CallerMemberName] string testName = null!)
    {
        var baselineDirectory = Path.Join(
            Path.GetDirectoryName(testPath)!,
            "TestFiles",
            Path.GetFileNameWithoutExtension(testPath)!,
            testName);
        Directory.CreateDirectory(baselineDirectory);
        var touchedFiles = new HashSet<string>();

        foreach (var source in result.GeneratedSources)
        {
            var baselinePath = Path.Join(baselineDirectory, source.HintName);
            GenerateOutputBaseline(baselinePath, in source);
            var baselineText = File.ReadAllText(baselinePath);
            AssertEx.EqualOrDiff(TrimChecksum(baselineText), TrimChecksum(source.SourceText.ToString()));
            Assert.True(touchedFiles.Add(baselinePath));
        }

        foreach (var file in Directory.EnumerateFiles(baselineDirectory))
        {
            if (!touchedFiles.Contains(file))
            {
                File.Delete(file);
            }
        }

        return result;
    }

    [Conditional("GENERATE_BASELINES")]
    private static void GenerateOutputBaseline(string baselinePath, in GeneratedSourceResult source)
    {
        var sourceText = source.SourceText.ToString();
        sourceText = sourceText.Replace("\r", "").Replace("\n", "\r\n");
        File.WriteAllText(baselinePath, sourceText, _baselineEncoding);
    }

    private static string GenerateExpectedOutput(GeneratorRunResult result)
    {
        StringBuilder sb = new StringBuilder("Generated Output:").AppendLine().AppendLine();
        for (int i = 0; i < result.GeneratedSources.Length; i++)
        {
            if (i > 0)
            {
                sb.AppendLine(",");
            }
            sb.Append("@\"").Append(result.GeneratedSources[i].SourceText.ToString().Replace("\"", "\"\"")).Append('"');
        }
        return sb.ToString();
    }

    public static GeneratorRunResult VerifyOutputsMatch(this GeneratorRunResult actual, GeneratorRunResult expected, params (int index, string replacement)[] diffs)
    {
        Assert.Equal(actual.GeneratedSources.Length, expected.GeneratedSources.Length);
        for (int i = 0; i < actual.GeneratedSources.Length; i++)
        {
            var diff = diffs.FirstOrDefault(p => p.index == i).replacement;
            if (diff is null)
            {
                var actualText = actual.GeneratedSources[i].SourceText.ToString();
                Assert.True(expected.GeneratedSources[i].SourceText.ToString() == actualText, $"No diff supplied. But index {i} was:\r\n\r\n{actualText.Replace("\"", "\"\"")}");
            }
            else
            {
                Assert.Equal(TrimChecksum(diff), TrimChecksum(actual.GeneratedSources[i].SourceText.ToString()));
            }
        }

        return actual;
    }

    private static string TrimChecksum(string text)
    {
        var trimmed = text.Trim('\r', '\n')                                // start and end
            .Replace("\r\n", "\r").Replace('\n', '\r').Replace('\r', '\n') // regular new-lines
            .Replace("\\r\\n", "\\n");                                     // embedded new-lines
        Assert.StartsWith("#pragma", trimmed);
        return trimmed.Substring(trimmed.IndexOf('\n') + 1);
    }
}
