﻿#pragma checksum "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml" "{8829d00f-11b8-4213-878b-770e8597ac16}" "4c55c366b179d82f3d2800004985646e9bf0edbf993e2f4a94cbb70079823905"
// <auto-generated/>
#pragma warning disable 1591
[assembly: global::Microsoft.AspNetCore.Razor.Hosting.RazorCompiledItemAttribute(typeof(AspNetCoreGeneratedDocument.TestFiles_IntegrationTests_CodeGenerationIntegrationTest_CSharp8), @"mvc.1.0.view", @"/TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml")]
namespace AspNetCoreGeneratedDocument
{
    #line default
    using global::System;
    using global::System.Linq;
    using global::System.Threading.Tasks;
    using global::Microsoft.AspNetCore.Mvc;
    using global::Microsoft.AspNetCore.Mvc.Rendering;
    using global::Microsoft.AspNetCore.Mvc.ViewFeatures;
#nullable restore
#line (1,2)-(1,34) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"
using System.Collections.Generic

#nullable disable
    ;
    #line default
    #line hidden
    [global::Microsoft.AspNetCore.Razor.Hosting.RazorSourceChecksumAttribute(@"Sha256", @"4c55c366b179d82f3d2800004985646e9bf0edbf993e2f4a94cbb70079823905", @"/TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml")]
    [global::Microsoft.AspNetCore.Razor.Hosting.RazorCompiledItemMetadataAttribute("Identifier", "/TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml")]
    [global::System.Runtime.CompilerServices.CreateNewOnMetadataUpdateAttribute]
    #nullable restore
    internal sealed class TestFiles_IntegrationTests_CodeGenerationIntegrationTest_CSharp8 : global::Microsoft.AspNetCore.Mvc.Razor.RazorPage<dynamic>
    #nullable disable
    {
        #pragma warning disable 1998
        public async override global::System.Threading.Tasks.Task ExecuteAsync()
        {
            WriteLiteral("\r\n");
#nullable restore
#line (3,3)-(23,1) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"

    IAsyncEnumerable<bool> GetAsyncEnumerable()
    {
        return null;
    }

    await foreach (var val in GetAsyncEnumerable())
    {

    }

    Range range = 1..5;
    using var disposable = GetLastDisposableInRange(range);

    var words = Array.Empty<string>();
    var testEnum = GetEnum();
    static TestEnum GetEnum()
    {
        return TestEnum.First;
    }

#line default
#line hidden
#nullable disable

            WriteLiteral("\r\n");
            Write(
#nullable restore
#line (25,2)-(25,13) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"
words[1..2]

#line default
#line hidden
#nullable disable
            );
            WriteLiteral("\r\n");
            Write(
#nullable restore
#line (26,3)-(26,16) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"
words[^2..^0]

#line default
#line hidden
#nullable disable
            );
            WriteLiteral("\r\n\r\n");
            Write(
#nullable restore
#line (28,3)-(33,2) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"
testEnum switch
{
    TestEnum.First => "The First!",
    TestEnum.Second => "The Second!",
    _ => "The others",
}

#line default
#line hidden
#nullable disable
            );
            WriteLiteral("\r\n\r\n");
#nullable restore
#line (35,2)-(37,5) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"
await foreach (var val in GetAsyncEnumerable())
{
    

#line default
#line hidden
#nullable disable

            Write(
#nullable restore
#line (37,6)-(37,9) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"
val

#line default
#line hidden
#nullable disable
            );
#nullable restore
#line (37,9)-(39,1) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"

}

#line default
#line hidden
#nullable disable

            WriteLiteral("\r\n");
            Write(
#nullable restore
#line (40,2)-(40,14) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"
Person!.Name

#line default
#line hidden
#nullable disable
            );
            WriteLiteral("\r\n");
            Write(
#nullable restore
#line (41,2)-(41,22) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"
People![0]!.Name![1]

#line default
#line hidden
#nullable disable
            );
            WriteLiteral("\r\n");
            Write(
#nullable restore
#line (42,2)-(42,23) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"
DoSomething!(Person!)

#line default
#line hidden
#nullable disable
            );
            WriteLiteral("\r\n\r\n");
        }
        #pragma warning restore 1998
#nullable restore
#line (44,13)-(67,1) "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/CSharp8.cshtml"

    enum TestEnum
    {
        First,
        Second
    }

    IDisposable GetLastDisposableInRange(Range range)
    {
        var disposables = (IDisposable[])ViewData["disposables"];
        return disposables[range][^1];
    }

    private Human? Person { get; set; }

    private Human?[]? People { get; set; }

    private Func<Human, string>? DoSomething { get; set; }

    private class Human
    {
        public string? Name { get; set; }
    }

#line default
#line hidden
#nullable disable

        #nullable restore
        [global::Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInjectAttribute]
        public global::Microsoft.AspNetCore.Mvc.ViewFeatures.IModelExpressionProvider ModelExpressionProvider { get; private set; } = default!;
        #nullable disable
        #nullable restore
        [global::Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInjectAttribute]
        public global::Microsoft.AspNetCore.Mvc.IUrlHelper Url { get; private set; } = default!;
        #nullable disable
        #nullable restore
        [global::Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInjectAttribute]
        public global::Microsoft.AspNetCore.Mvc.IViewComponentHelper Component { get; private set; } = default!;
        #nullable disable
        #nullable restore
        [global::Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInjectAttribute]
        public global::Microsoft.AspNetCore.Mvc.Rendering.IJsonHelper Json { get; private set; } = default!;
        #nullable disable
        #nullable restore
        [global::Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInjectAttribute]
        public global::Microsoft.AspNetCore.Mvc.Rendering.IHtmlHelper<dynamic> Html { get; private set; } = default!;
        #nullable disable
    }
}
#pragma warning restore 1591
