using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Newtonsoft.Json.Serialization;
using System;
using System.Reflection;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class RazorSerializationBinder : DefaultSerializationBinder
{
    private readonly Assembly _assembly = typeof(IntermediateNode).Assembly;

    public override void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
    {
        assemblyName = null;
        typeName = null;

        if (typeof(IntermediateNode).IsAssignableFrom(serializedType))
        {
            typeName = serializedType.Name;
        }
    }

    public override Type BindToType(string? assemblyName, string typeName)
    {
        return _assembly.GetType("Microsoft.AspNetCore.Razor.Language.Intermediate." + typeName) ??
            _assembly.GetType("Microsoft.AspNetCore.Razor.Language.Extensions." + typeName);
    }
}
