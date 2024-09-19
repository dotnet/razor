// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;

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

    [IntegrationTestFact]
    public void DefineAndUndef()
    {
        var generated = CompileToCSharp("""
            @{
            #define SomeSymbol
            #undef SomeSymbol
            }
            """);

        AssertDocumentNodeMatchesBaseline(generated.CodeDocument);
        AssertCSharpDocumentMatchesBaseline(generated.CodeDocument);
        CompileToAssembly(generated,
                // x:\dir\subdir\Test\TestComponent.cshtml(2,2): error CS1032: Cannot define/undefine preprocessor symbols after first token in file
                // #define SomeSymbol
                Diagnostic(ErrorCode.ERR_PPDefFollowsToken, "define").WithLocation(2, 2),
                // x:\dir\subdir\Test\TestComponent.cshtml(3,2): error CS1032: Cannot define/undefine preprocessor symbols after first token in file
                // #undef SomeSymbol
                Diagnostic(ErrorCode.ERR_PPDefFollowsToken, "undef").WithLocation(3, 2)
        );
    }

    [IntegrationTestFact]
    public void StartOfLine_01()
    {
        var generated = CompileToCSharp("""
            @{ #if true }
            @{ #endif }
            """);

        AssertDocumentNodeMatchesBaseline(generated.CodeDocument);
        AssertCSharpDocumentMatchesBaseline(generated.CodeDocument);
        CompileToAssembly(generated,
                        // x:\dir\subdir\Test\TestComponent.cshtml(1,13): error CS1025: Single-line comment or end-of-line expected
                //    #if true }
                Diagnostic(ErrorCode.ERR_EndOfPPLineExpected, "}").WithLocation(1, 13),
                // x:\dir\subdir\Test\TestComponent.cshtml(2,11): error CS1025: Single-line comment or end-of-line expected
                //    #endif }
                Diagnostic(ErrorCode.ERR_EndOfPPLineExpected, "}").WithLocation(2, 11));
    }

    // PROTOTYPE: More line tests
}
