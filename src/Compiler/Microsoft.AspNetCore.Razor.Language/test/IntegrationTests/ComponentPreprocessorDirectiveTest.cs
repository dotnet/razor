// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Test.Common;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

public class ComponentPreprocessorDirectiveTest(bool designTime = false)
        : RazorBaselineIntegrationTestBase(layer: TestProject.Layer.Compiler)
{
    internal override string FileKind => FileKinds.Component;

    internal override bool UseTwoPhaseCompilation => true;

    internal string ComponentName = "TestComponent";

    internal override string DefaultFileName => ComponentName + ".cshtml";

    internal override bool DesignTime => designTime;

    protected override string GetDirectoryPath(string testName)
    {
        var directory = DesignTime ? "ComponentDesignTimePreprocessorDirectiveTest" : "ComponentRuntimePreprocessorDirectiveTest";
        return $"TestFiles/IntegrationTests/{directory}/{testName}";
    }

    [IntegrationTestFact]
    public void IfDefAndPragma()
    {
        var generated = CompileToCSharp("""
            @{
            #pragma warning disable 219 // variable declared but not used
            #if true
                var x = 1;
            #endif
            }
            """);

        AssertDocumentNodeMatchesBaseline(generated.CodeDocument);
        AssertCSharpDocumentMatchesBaseline(generated.CodeDocument);
        CompileToAssembly(generated);
    }

    [IntegrationTestFact]
    public void DisabledText_01()
    {
        var generated = CompileToCSharp("""
            @{
            #if false
                <p>Some text</p>
            #endif
            }
            """);

        AssertDocumentNodeMatchesBaseline(generated.CodeDocument);
        AssertCSharpDocumentMatchesBaseline(generated.CodeDocument);
        CompileToAssembly(generated);
    }

    [IntegrationTestFact]
    public void PassParseOptionsThrough_01()
    {
        var parseOptions = CSharpParseOptions.WithPreprocessorSymbols("SomeSymbol");

        var generated = CompileToCSharp("""
            @{
            #if SomeSymbol
                <p>Some text</p>
            #endif
            }
            """,
            csharpParseOptions: parseOptions);

        AssertDocumentNodeMatchesBaseline(generated.CodeDocument);
        AssertCSharpDocumentMatchesBaseline(generated.CodeDocument);
        CompileToAssembly(generated);
    }

    [IntegrationTestFact]
    public void PassParseOptionsThrough_02()
    {
        var parseOptions = CSharpParseOptions.WithPreprocessorSymbols("SomeSymbol");

        var generated = CompileToCSharp("""
            @{
            #if !SomeSymbol
                <p>Some text</p>
            #endif
            }
            """,
            csharpParseOptions: parseOptions);

        AssertDocumentNodeMatchesBaseline(generated.CodeDocument);
        AssertCSharpDocumentMatchesBaseline(generated.CodeDocument);
        CompileToAssembly(generated);
    }
}
