// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Razor.Compiler.Analyzers;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.Analyzers.Tests
{
    public class ComponentParameterNullableWarningSuppressorTests
    {
        [Fact]
        public async Task ParameterEditorRequiredNoWarning()
        {
            var testCode = """
                #nullable enable
                using System;
                using Microsoft.AspNetCore.Components;

                public class MyComponent : ComponentBase
                {
                    [Parameter, EditorRequired]
                    public string MyParameter { get; set; }
                }
                """;

            await VerifyAnalyzerAsync(testCode,
                // /0/Test0.cs(9,19): warning CS8618: Non-nullable property 'MyParameter' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
                DiagnosticResult.CompilerWarning("CS8618").WithSpan(8, 19, 8, 30).WithSpan(8, 19, 8, 30).WithArguments("property", "MyParameter").WithIsSuppressed(true)
                );
        }

        [Fact]
        public async Task NoEditorRequiredStillReports()
        {
            var testCode = """
                #nullable enable
                using System;
                using Microsoft.AspNetCore.Components;

                public class MyComponent : ComponentBase
                {
                    [Parameter]
                    public string MyParameter { get; set; }
                }
                """;

            await VerifyAnalyzerAsync(testCode,
                // /0/Test0.cs(9,19): warning CS8618: Non-nullable property 'MyParameter' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
                DiagnosticResult.CompilerWarning("CS8618").WithSpan(8, 19, 8, 30).WithSpan(8, 19, 8, 30).WithArguments("property", "MyParameter")
                );
        }

        [Fact]
        public async Task NoParameterRequiredStillReports()
        {
            var testCode = """
                #nullable enable
                using System;
                using Microsoft.AspNetCore.Components;

                public class MyComponent : ComponentBase
                {
                    [EditorRequired]
                    public string MyParameter { get; set; }
                }
                """;

            await VerifyAnalyzerAsync(testCode,
                // /0/Test0.cs(9,19): warning CS8618: Non-nullable property 'MyParameter' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
                DiagnosticResult.CompilerWarning("CS8618").WithSpan(8, 19, 8, 30).WithSpan(8, 19, 8, 30).WithArguments("property", "MyParameter")
                );
        }

        [Fact]
        public async Task AliasedAttributes()
        {
            var testCode = """
                #nullable enable
                using System;
                using Microsoft.AspNetCore.Components;
                using MyParameter = Microsoft.AspNetCore.Components.ParameterAttribute;
                using MyRequired = Microsoft.AspNetCore.Components.EditorRequiredAttribute;

                public class MyComponent : ComponentBase
                {
                    [MyParameter, MyRequired]
                    public string MyParameter { get; set; }
                }
                """;

            await VerifyAnalyzerAsync(testCode,
                // /0/Test0.cs(9,19): warning CS8618: Non-nullable property 'MyParameter' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
                DiagnosticResult.CompilerWarning("CS8618").WithSpan(10, 19, 10, 30).WithSpan(10, 19, 10, 30).WithArguments("property", "MyParameter").WithIsSuppressed(true)
                );
        }

        [Fact]
        public async Task LocallyDefinedAttributes()
        {
            var testCode = """

                #nullable enable
                using System;
                using Microsoft.AspNetCore.Components;
                
                public class MyComponent
                {
                    [Parameter, EditorRequired]
                    public string MyParameter { get; set; }
                }

                namespace Microsoft.AspNetCore.Components
                {
                    public class ParameterAttribute : Attribute { }
                    public class EditorRequiredAttribute : Attribute { }
                }

                """;

            await VerifyAnalyzerAsync(testCode, extraReferences: [],
                // /0/Test0.cs(9,19): warning CS8618: Non-nullable property 'MyParameter' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
                DiagnosticResult.CompilerWarning("CS8618").WithSpan(9, 19, 9, 30).WithSpan(9, 19, 9, 30).WithArguments("property", "MyParameter").WithIsSuppressed(true)
                );
        }

        [Fact]
        public async Task LocallyDefinedAttributesDifferentNamespace()
        {
            var testCode = """

                #nullable enable
                using System;
                using MyNamespace;
                
                public class MyComponent
                {
                    [Parameter, EditorRequired]
                    public string MyParameter { get; set; }
                }

                namespace MyNamespace
                {
                    public class ParameterAttribute : Attribute { }
                    public class EditorRequiredAttribute : Attribute { }
                }

                """;

            await VerifyAnalyzerAsync(testCode, extraReferences: [],
                // /0/Test0.cs(9,19): warning CS8618: Non-nullable property 'MyParameter' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
                DiagnosticResult.CompilerWarning("CS8618").WithSpan(9, 19, 9, 30).WithSpan(9, 19, 9, 30).WithArguments("property", "MyParameter")
                );
        }

        private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
            => VerifyAnalyzerAsync(source,
                                   Basic.Reference.Assemblies.AspNet80.References.All,
                                   expected);

        private static async Task VerifyAnalyzerAsync(string source, ImmutableArray<PortableExecutableReference> extraReferences, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ComponentParameterNullableWarningSuppressor, DefaultVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                CompilerDiagnostics = CompilerDiagnostics.Warnings,
                DisabledDiagnostics = { "CS1591" }
            };

            test.TestState.AdditionalReferences.AddRange(extraReferences);
            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }
    }
}
