﻿// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line default
    using global::System;
    using global::System.Collections.Generic;
    using global::System.Linq;
    using global::System.Threading.Tasks;
    using global::Microsoft.AspNetCore.Components;
    #line default
    #line hidden
    #nullable restore
    public partial class TestComponent : global::Microsoft.AspNetCore.Components.ComponentBase
    #nullable disable
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            global::__Blazor.Test.TestComponent.TypeInference.CreateMyComponent_0(__builder, 0, 1, 
#nullable restore
#line (2,20)-(2,24) "x:\dir\subdir\Test\TestComponent.cshtml"
true

#line default
#line hidden
#nullable disable
            , 2, "str", 3, 
#nullable restore
#line (4,24)-(4,33) "x:\dir\subdir\Test\TestComponent.cshtml"
() => { }

#line default
#line hidden
#nullable disable
            , 4, 
#nullable restore
#line (5,22)-(5,23) "x:\dir\subdir\Test\TestComponent.cshtml"
c

#line default
#line hidden
#nullable disable
            , 5, 
#nullable restore
#line (1,33)-(1,34) "x:\dir\subdir\Test\TestComponent.cshtml"
c

#line default
#line hidden
#nullable disable
            , 6, global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, global::Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.CreateInferredEventCallback(this, __value => c = __value, c)));
        }
        #pragma warning restore 1998
#nullable restore
#line (7,8)-(9,1) "x:\dir\subdir\Test\TestComponent.cshtml"

    private MyClass<string> c = new();

#line default
#line hidden
#nullable disable

    }
}
namespace __Blazor.Test.TestComponent
{
    #line hidden
    internal static class TypeInference
    {
        public static void CreateMyComponent_0<T>(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder, int seq, int __seq0, global::System.Boolean __arg0, int __seq1, global::System.String __arg1, int __seq2, global::System.Delegate __arg2, int __seq3, global::System.Object __arg3, int __seq4, global::Test.MyClass<T> __arg4, int __seq5, global::Microsoft.AspNetCore.Components.EventCallback<global::Test.MyClass<T>> __arg5)
        {
        __builder.OpenComponent<global::Test.MyComponent<T>>(seq);
        __builder.AddAttribute(__seq0, nameof(global::Test.MyComponent<T>.
#nullable restore
#line (2,5)-(2,18) "x:\dir\subdir\Test\TestComponent.cshtml"
BoolParameter

#line default
#line hidden
#nullable disable
        ), (object)__arg0);
        __builder.AddAttribute(__seq1, nameof(global::Test.MyComponent<T>.
#nullable restore
#line (3,5)-(3,20) "x:\dir\subdir\Test\TestComponent.cshtml"
StringParameter

#line default
#line hidden
#nullable disable
        ), (object)__arg1);
        __builder.AddAttribute(__seq2, nameof(global::Test.MyComponent<T>.
#nullable restore
#line (4,5)-(4,22) "x:\dir\subdir\Test\TestComponent.cshtml"
DelegateParameter

#line default
#line hidden
#nullable disable
        ), (object)__arg2);
        __builder.AddAttribute(__seq3, nameof(global::Test.MyComponent<T>.
#nullable restore
#line (5,5)-(5,20) "x:\dir\subdir\Test\TestComponent.cshtml"
ObjectParameter

#line default
#line hidden
#nullable disable
        ), (object)__arg3);
        __builder.AddAttribute(__seq4, nameof(global::Test.MyComponent<T>.
#nullable restore
#line (1,20)-(1,31) "x:\dir\subdir\Test\TestComponent.cshtml"
MyParameter

#line default
#line hidden
#nullable disable
        ), (object)__arg4);
        __builder.AddAttribute(__seq5, nameof(global::Test.MyComponent<T>.MyParameterChanged), (object)__arg5);
        __builder.CloseComponent();
        }
    }
}
#pragma warning restore 1591
