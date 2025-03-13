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
                DiagnosticResult.CompilerWarning("CS8618").WithSpan(8, 19, 8, 30).WithSpan(8, 19, 8, 30).WithArguments("property", "MyParameter").WithIsSuppressed(true)
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
                DiagnosticResult.CompilerWarning("CS8618").WithSpan(8, 19, 8, 30).WithSpan(8, 19, 8, 30).WithArguments("property", "MyParameter")
                );
        }

        [Fact]
        public async Task LocallyDefinedAttributesAndSdkAttributes()
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

            await VerifyAnalyzerAsync(testCode,
                    // /0/Test0.cs(8,19): warning CS8618: Non-nullable property 'MyParameter' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
                    DiagnosticResult.CompilerWarning("CS8618").WithSpan(8, 19, 8, 30).WithSpan(8, 19, 8, 30).WithArguments("property", "MyParameter").WithIsSuppressed(true),
                    // /0/Test0.cs(7,6): warning CS0436: The type 'ParameterAttribute' in '/0/Test0.cs' conflicts with the imported type 'ParameterAttribute' in 'Microsoft.AspNetCore.Components, Version=8.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60'. Using the type defined in '/0/Test0.cs'.
                    DiagnosticResult.CompilerWarning("CS0436").WithSpan(7, 6, 7, 15).WithArguments("/0/Test0.cs", "Microsoft.AspNetCore.Components.ParameterAttribute", "Microsoft.AspNetCore.Components, Version=8.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "Microsoft.AspNetCore.Components.ParameterAttribute"),
                    // /0/Test0.cs(7,17): warning CS0436: The type 'EditorRequiredAttribute' in '/0/Test0.cs' conflicts with the imported type 'EditorRequiredAttribute' in 'Microsoft.AspNetCore.Components, Version=8.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60'. Using the type defined in '/0/Test0.cs'.
                    DiagnosticResult.CompilerWarning("CS0436").WithSpan(7, 17, 7, 31).WithArguments("/0/Test0.cs", "Microsoft.AspNetCore.Components.EditorRequiredAttribute", "Microsoft.AspNetCore.Components, Version=8.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "Microsoft.AspNetCore.Components.EditorRequiredAttribute")
                );
        }

        [Theory]
        [InlineData("internal")]
        [InlineData("private")]
        [InlineData("protected internal")]
        [InlineData("protected")]
        [InlineData("public static")]
        public async Task IncorrectModifiersStillReport(string modifiers)
        {
            var testCode = $$"""
                #nullable enable
                using System;
                using Microsoft.AspNetCore.Components;

                public class MyComponent : ComponentBase
                {
                    [Parameter, EditorRequired]
                    {{modifiers}}
                    string MyParameter { get; set; }
                }
                """;

            await VerifyAnalyzerAsync(testCode,
                // /0/Test0.cs(9,19): warning CS8618: Non-nullable property 'MyParameter' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
                DiagnosticResult.CompilerWarning("CS8618").WithSpan(9, 12, 9, 23).WithSpan(9, 12, 9, 23).WithArguments("property", "MyParameter")
                );
        }

        [Theory]
        [InlineData("")]
        [InlineData("init;")]
        [InlineData("private set;")]
        [InlineData("private init;")]
        public async Task IncorrectSetterStillReport(string setter)
        {
            var testCode = $$"""
                #nullable enable
                using System;
                using Microsoft.AspNetCore.Components;

                public class MyComponent : ComponentBase
                {
                    [Parameter, EditorRequired]
                    public string MyParameter { get; {{setter}} }
                }
                """;

            await VerifyAnalyzerAsync(testCode,
                // /0/Test0.cs(9,19): warning CS8618: Non-nullable property 'MyParameter' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
                DiagnosticResult.CompilerWarning("CS8618").WithSpan(8, 19, 8, 30).WithSpan(8, 19, 8, 30).WithArguments("property", "MyParameter")
                );
        }

        [Fact]
        public async Task RequiredPropertyDoesNotReport()
        {
            var testCode = $$"""
                #nullable enable
                using System;
                using Microsoft.AspNetCore.Components;

                public class MyComponent : ComponentBase
                {
                    [Parameter, EditorRequired]
                    public required string MyParameter { get; set; }
                }
                """;

            await VerifyAnalyzerAsync(testCode);
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
                DisabledDiagnostics = { "CS1591" }, // Missing XML comment for publicly visible type or member
            };

            test.TestState.AdditionalReferences.AddRange(extraReferences);
            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }
    }
}
