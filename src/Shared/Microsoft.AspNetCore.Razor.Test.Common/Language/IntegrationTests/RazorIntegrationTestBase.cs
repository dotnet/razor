﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

public class RazorIntegrationTestBase
{
    internal const string ArbitraryWindowsPath = "x:\\dir\\subdir\\Test";
    internal const string ArbitraryMacLinuxPath = "/dir/subdir/Test";

    // Creating the initial compilation + reading references is on the order of 250ms without caching
    // so making sure it doesn't happen for each test.
    protected static readonly CSharpCompilation DefaultBaseCompilation;

    private static CSharpParseOptions CSharpParseOptions { get; }

    static RazorIntegrationTestBase()
    {
        DefaultBaseCompilation = CSharpCompilation.Create(
            "TestAssembly",
            Array.Empty<SyntaxTree>(),
            ReferenceUtil.AspNetLatestAll,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpParseOptions = new CSharpParseOptions(LanguageVersion.Preview);
    }

    public RazorIntegrationTestBase()
    {
        AdditionalSyntaxTrees = new List<SyntaxTree>();
        AdditionalRazorItems = new List<RazorProjectItem>();
        ImportItems = new List<RazorProjectItem>();

        BaseCompilation = DefaultBaseCompilation;
        Configuration = RazorConfiguration.Default;
        FileSystem = new VirtualRazorProjectFileSystem();
        PathSeparator = Path.DirectorySeparatorChar.ToString();
        WorkingDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ArbitraryWindowsPath : ArbitraryMacLinuxPath;

        DefaultRootNamespace = "Test"; // Matches the default working directory
        DefaultFileName = "TestComponent.cshtml";
    }

    internal List<RazorProjectItem> AdditionalRazorItems { get; }

    internal List<RazorProjectItem> ImportItems { get; }

    internal List<SyntaxTree> AdditionalSyntaxTrees { get; }

    internal virtual CSharpCompilation BaseCompilation { get; }

    internal virtual RazorConfiguration Configuration { get; }

    internal string DefaultRootNamespace { get; set; }

    internal virtual string DefaultFileName { get; }

    internal string DefaultDocumentPath => WorkingDirectory + PathSeparator + DefaultFileName;

    internal virtual bool DesignTime { get; }

    internal virtual bool DeclarationOnly { get; }

    /// <summary>
    /// Gets a hardcoded document kind to be added to each code document that's created. This can
    /// be used to generate components.
    /// </summary>
    internal virtual string FileKind { get; }

    internal virtual VirtualRazorProjectFileSystem FileSystem { get; }

    // Used to force a specific style of line-endings for testing. This matters
    // for the baseline tests that exercise line mappings. Even though we normalize
    // newlines for testing, the difference between platforms affects the data through
    // the *count* of characters written.
    internal virtual string LineEnding { get; }

    internal virtual string PathSeparator { get; }

    internal virtual bool NormalizeSourceLineEndings { get; }

    internal virtual bool UseTwoPhaseCompilation { get; }

    internal virtual string WorkingDirectory { get; }

    // intentionally private - we don't want individual tests messing with the project engine
    private RazorProjectEngine CreateProjectEngine(RazorConfiguration configuration, MetadataReference[] references, bool supportLocalizedComponentNames)
    {
        return RazorProjectEngine.Create(configuration, FileSystem, b =>
        {
            b.SetRootNamespace(DefaultRootNamespace);

            // Turn off checksums, we're testing code generation.
            b.Features.Add(new SuppressChecksum());

            if (supportLocalizedComponentNames)
            {
                b.Features.Add(new SupportLocalizedComponentNames());
            }

            b.Features.Add(new TestImportProjectFeature(ImportItems));

            if (LineEnding != null)
            {
                b.Features.Add(new SetNewLineOptionFeature(LineEnding));
            }

            b.Features.Add(new SuppressUniqueIdsPhase());

            b.Features.Add(new CompilationTagHelperFeature());
            b.Features.Add(new DefaultMetadataReferenceFeature()
            {
                References = references,
            });

            b.SetCSharpLanguageVersion(CSharpParseOptions.LanguageVersion);

            CompilerFeatures.Register(b);
        });
    }

    internal RazorProjectItem CreateProjectItem(string cshtmlRelativePath, string cshtmlContent, string fileKind = null, string cssScope = null)
    {
        var fullPath = WorkingDirectory + PathSeparator + cshtmlRelativePath;

        // FilePaths in Razor are **always** are of the form '/a/b/c.cshtml'
        var filePath = cshtmlRelativePath.Replace('\\', '/');
        if (!filePath.StartsWith("/", StringComparison.Ordinal))
        {
            filePath = '/' + filePath;
        }

        if (NormalizeSourceLineEndings)
        {
            cshtmlContent = cshtmlContent.Replace("\r", "").Replace("\n", LineEnding);
        }

        return new TestRazorProjectItem(
            filePath: filePath,
            physicalPath: fullPath,
            relativePhysicalPath: cshtmlRelativePath,
            basePath: WorkingDirectory,
            fileKind: fileKind ?? FileKind,
            cssScope: cssScope)
        {
            Content = cshtmlContent.TrimStart(),
        };
    }

    protected CompileToCSharpResult CompileToCSharp(string cshtmlContent, params DiagnosticDescription[] expectedCSharpDiagnostics)
    {
        return CompileToCSharp(
            DefaultFileName,
            cshtmlContent: cshtmlContent,
            expectedCSharpDiagnostics: expectedCSharpDiagnostics);
    }

    protected CompileToCSharpResult CompileToCSharp(
        string cshtmlContent,
        string cssScope = null,
        bool supportLocalizedComponentNames = false,
        bool nullableEnable = false,
        RazorConfiguration configuration = null,
        CSharpCompilation baseCompilation = null,
        params DiagnosticDescription[] expectedCSharpDiagnostics)
    {
        return CompileToCSharp(
            DefaultFileName,
            cshtmlContent,
            cssScope: cssScope,
            supportLocalizedComponentNames: supportLocalizedComponentNames,
            nullableEnable: nullableEnable,
            configuration: configuration,
            baseCompilation: baseCompilation,
            expectedCSharpDiagnostics: expectedCSharpDiagnostics);
    }

    protected CompileToCSharpResult CompileToCSharp(
        string cshtmlRelativePath,
        string cshtmlContent,
        string fileKind = null,
        string cssScope = null,
        bool supportLocalizedComponentNames = false,
        bool nullableEnable = false,
        RazorConfiguration configuration = null,
        CSharpCompilation baseCompilation = null,
        params DiagnosticDescription[] expectedCSharpDiagnostics)
    {
        if (DeclarationOnly && DesignTime)
        {
            throw new InvalidOperationException($"{nameof(DeclarationOnly)} cannot be used with {nameof(DesignTime)}.");
        }

        if (DeclarationOnly && UseTwoPhaseCompilation)
        {
            throw new InvalidOperationException($"{nameof(DeclarationOnly)} cannot be used with {nameof(UseTwoPhaseCompilation)}.");
        }

        baseCompilation ??= BaseCompilation;
        configuration ??= Configuration;

        if (nullableEnable)
        {
            baseCompilation = baseCompilation.WithOptions(baseCompilation.Options.WithNullableContextOptions(NullableContextOptions.Enable));
        }

        configuration = configuration ?? this.Configuration;

        if (UseTwoPhaseCompilation)
        {
            // The first phase won't include any metadata references for component discovery. This mirrors
            // what the build does.
            var projectEngine = CreateProjectEngine(configuration, Array.Empty<MetadataReference>(), supportLocalizedComponentNames);

            RazorCodeDocument codeDocument;
            foreach (var item in AdditionalRazorItems)
            {
                // Result of generating declarations
                codeDocument = projectEngine.ProcessDeclarationOnly(item);
                Assert.Empty(codeDocument.GetCSharpDocument().Diagnostics);

                var syntaxTree = Parse(codeDocument.GetCSharpDocument().GeneratedCode, path: item.FilePath);
                AdditionalSyntaxTrees.Add(syntaxTree);
            }

            // Result of generating declarations
            var projectItem = CreateProjectItem(cshtmlRelativePath, cshtmlContent, fileKind, cssScope);
            codeDocument = projectEngine.ProcessDeclarationOnly(projectItem);
            var declaration = new CompileToCSharpResult
            {
                BaseCompilation = baseCompilation.AddSyntaxTrees(AdditionalSyntaxTrees),
                CodeDocument = codeDocument,
                Code = codeDocument.GetCSharpDocument().GeneratedCode,
                RazorDiagnostics = codeDocument.GetCSharpDocument().Diagnostics,
            };

            // Result of doing 'temp' compilation
            var tempAssembly = CompileToAssembly(declaration, expectedDiagnostics: expectedCSharpDiagnostics);

            // Add the 'temp' compilation as a metadata reference
            var references = baseCompilation.References.Concat(new[] { tempAssembly.Compilation.ToMetadataReference() }).ToArray();
            projectEngine = CreateProjectEngine(configuration, references, supportLocalizedComponentNames);

            // Now update the any additional files
            foreach (var item in AdditionalRazorItems)
            {
                // Result of generating definition
                codeDocument = DesignTime ? projectEngine.ProcessDesignTime(item) : projectEngine.Process(item);
                Assert.Empty(codeDocument.GetCSharpDocument().Diagnostics);

                // Replace the 'declaration' syntax tree
                var syntaxTree = Parse(codeDocument.GetCSharpDocument().GeneratedCode, path: item.FilePath);
                AdditionalSyntaxTrees.RemoveAll(st => st.FilePath == item.FilePath);
                AdditionalSyntaxTrees.Add(syntaxTree);
            }

            // Result of real code generation for the document under test
            codeDocument = DesignTime ? projectEngine.ProcessDesignTime(projectItem) : projectEngine.Process(projectItem);
            return new CompileToCSharpResult
            {
                BaseCompilation = baseCompilation.AddSyntaxTrees(AdditionalSyntaxTrees),
                CodeDocument = codeDocument,
                Code = codeDocument.GetCSharpDocument().GeneratedCode,
                RazorDiagnostics = codeDocument.GetCSharpDocument().Diagnostics,
            };
        }
        else
        {
            // For single phase compilation tests just use the base compilation's references.
            // This will include the built-in components.
            var projectEngine = CreateProjectEngine(configuration, baseCompilation.References.ToArray(), supportLocalizedComponentNames);

            var projectItem = CreateProjectItem(cshtmlRelativePath, cshtmlContent, fileKind, cssScope);

            RazorCodeDocument codeDocument;
            if (DeclarationOnly)
            {
                codeDocument = projectEngine.ProcessDeclarationOnly(projectItem);
            }
            else if (DesignTime)
            {
                codeDocument = projectEngine.ProcessDesignTime(projectItem);
            }
            else
            {
                codeDocument = projectEngine.Process(projectItem);
            }

            return new CompileToCSharpResult
            {
                BaseCompilation = baseCompilation.AddSyntaxTrees(AdditionalSyntaxTrees),
                CodeDocument = codeDocument,
                Code = codeDocument.GetCSharpDocument().GeneratedCode,
                RazorDiagnostics = codeDocument.GetCSharpDocument().Diagnostics,
            };
        }
    }

    protected CompileToAssemblyResult CompileToAssembly(string cshtmlRelativePath, string cshtmlContent)
    {
        var cSharpResult = CompileToCSharp(cshtmlRelativePath, cshtmlContent: cshtmlContent);
        return CompileToAssembly(cSharpResult);
    }

    protected CompileToAssemblyResult CompileToAssembly(CompileToCSharpResult cSharpResult, params DiagnosticDescription[] expectedDiagnostics)
    {
        var syntaxTrees = new[]
        {
            Parse(cSharpResult.Code),
        };

        var compilation = cSharpResult.BaseCompilation.AddSyntaxTrees(syntaxTrees);

        var diagnostics = compilation
            .GetDiagnostics()
            .Where(d => d.Severity != DiagnosticSeverity.Hidden);

        diagnostics.Verify(expectedDiagnostics);

        if (diagnostics.Any())
        {
            return new CompileToAssemblyResult
            {
                Compilation = compilation,
                CSharpDiagnostics = diagnostics,
            };
        }

        using (var peStream = new MemoryStream())
        {
            var emitResult = compilation.Emit(peStream);
            if (!emitResult.Success)
            {
                throw new CompilationFailedException(compilation, emitResult.Diagnostics);
            }

            return new CompileToAssemblyResult
            {
                Compilation = compilation,
                CSharpDiagnostics = diagnostics,
            };
        }
    }

    protected INamedTypeSymbol CompileToComponent(string cshtmlSource)
    {
        var assemblyResult = CompileToAssembly(DefaultFileName, cshtmlSource);

        var componentFullTypeName = $"{DefaultRootNamespace}.{Path.GetFileNameWithoutExtension(DefaultFileName)}";
        return CompileToComponent(assemblyResult, componentFullTypeName);
    }

    protected INamedTypeSymbol CompileToComponent(CompileToCSharpResult cSharpResult, string fullTypeName)
    {
        return CompileToComponent(CompileToAssembly(cSharpResult), fullTypeName);
    }

    protected INamedTypeSymbol CompileToComponent(CompileToAssemblyResult assemblyResult, string fullTypeName)
    {
        var componentType = assemblyResult.Compilation.GetTypeByMetadataName(fullTypeName);
        if (componentType == null)
        {
            throw new XunitException($"Failed to find component type '{fullTypeName}'");
        }

        return componentType;
    }

    protected static CSharpSyntaxTree Parse(string text, string path = null)
    {
        return (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(text, CSharpParseOptions, path: path);
    }

    protected static void AssertSourceEquals(string expected, CompileToCSharpResult generated)
    {
        // Normalize the paths inside the expected result to match the OS paths
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var windowsPath = Path.Combine(ArbitraryWindowsPath, generated.CodeDocument.Source.RelativePath).Replace('/', '\\');
            expected = expected.Replace(windowsPath, generated.CodeDocument.Source.FilePath);
        }

        expected = expected.Trim();
        Assert.Equal(expected, generated.Code.Trim(), ignoreLineEndingDifferences: true);
    }

    protected class CompileToCSharpResult
    {
        // A compilation that can be used *with* this code to compile an assembly
        public Compilation BaseCompilation { get; set; }
        public RazorCodeDocument CodeDocument { get; set; }
        public string Code { get; set; }
        public IEnumerable<RazorDiagnostic> RazorDiagnostics { get; set; }
    }

    protected class CompileToAssemblyResult
    {
        public Compilation Compilation { get; set; }
        public string VerboseLog { get; set; }
        public IEnumerable<Diagnostic> CSharpDiagnostics { get; set; }
    }

    private class CompilationFailedException : XunitException
    {
        public CompilationFailedException(Compilation compilation, ImmutableArray<Diagnostic> diagnostics = default)
            : base(userMessage: "Compilation failed")
        {
            Compilation = compilation;
            Diagnostics = diagnostics.NullToEmpty();
        }

        public Compilation Compilation { get; }
        public ImmutableArray<Diagnostic> Diagnostics { get; }

        public override string Message
        {
            get
            {
                var builder = new StringBuilder();
                builder.AppendLine("Compilation failed: ");

                var diagnostics = Compilation.GetDiagnostics().Concat(Diagnostics);
                var syntaxTreesWithErrors = new HashSet<SyntaxTree>();
                foreach (var diagnostic in diagnostics)
                {
                    builder.AppendLine(diagnostic.ToString());

                    if (diagnostic.Location.IsInSource)
                    {
                        syntaxTreesWithErrors.Add(diagnostic.Location.SourceTree);
                    }
                }

                if (syntaxTreesWithErrors.Any())
                {
                    builder.AppendLine();
                    builder.AppendLine();

                    foreach (var syntaxTree in syntaxTreesWithErrors)
                    {
                        builder.AppendLine($"File {syntaxTree.FilePath ?? "unknown"}:");
                        builder.AppendLine(syntaxTree.GetText().ToString());
                    }
                }

                return builder.ToString();
            }
        }
    }

    private class SuppressChecksum : IConfigureRazorCodeGenerationOptionsFeature
    {
        public int Order => 0;

        public RazorEngine Engine { get; set; }

        public void Configure(RazorCodeGenerationOptionsBuilder options)
        {
            options.SuppressChecksum = true;
        }
    }

    private class SupportLocalizedComponentNames : IConfigureRazorCodeGenerationOptionsFeature
    {
        public int Order => 0;

        public RazorEngine Engine { get; set; }

        public void Configure(RazorCodeGenerationOptionsBuilder options)
        {
            options.SupportLocalizedComponentNames = true;
        }
    }

    private sealed class SetNewLineOptionFeature(string newLine) : RazorEngineFeatureBase, IConfigureRazorCodeGenerationOptionsFeature
    {
        public int Order { get; }

        public void Configure(RazorCodeGenerationOptionsBuilder options)
        {
            options.NewLine = newLine;
        }
    }

    private sealed class SuppressUniqueIdsPhase : RazorEngineFeatureBase, IConfigureRazorCodeGenerationOptionsFeature
    {
        public int Order { get; }

        public void Configure(RazorCodeGenerationOptionsBuilder options)
        {
            options.SuppressUniqueIds = "__UniqueIdSuppressedForTesting__";
        }
    }

    private class TestImportProjectFeature : IImportProjectFeature
    {
        private readonly List<RazorProjectItem> _imports;

        public TestImportProjectFeature(List<RazorProjectItem> imports)
        {
            _imports = imports;
        }

        public RazorProjectEngine ProjectEngine { get; set; }

        public IReadOnlyList<RazorProjectItem> GetImports(RazorProjectItem projectItem)
        {
            return _imports;
        }
    }
}
