using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Reflection;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class RazorContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        if (property.DeclaringType == typeof(LazyIntermediateToken))
        {
            if (property.PropertyName == nameof(LazyIntermediateToken.Content))
            {
                property.Writable = true;
                property.ValueProvider = new DelegateValueProvider<LazyIntermediateToken, string>(
                    static token => token.Content,
                    static (token, content) => token.Content = content);
            }
            else
            {
                property.Ignored = true;
            }
        }

        if (property.DeclaringType == typeof(DocumentIntermediateNode) && property.PropertyName == nameof(DocumentIntermediateNode.Target))
        {
            property.Ignored = true;
        }

        return property;
    }

    private class DelegateValueProvider<TTarget, TValue> : IValueProvider
    {
        private readonly Func<TTarget, TValue?> _getter;
        private readonly Action<TTarget, TValue?> _setter;

        public DelegateValueProvider(Func<TTarget, TValue?> getter, Action<TTarget, TValue?> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public object? GetValue(object target)
        {
            return _getter((TTarget)target);
        }

        public void SetValue(object target, object? value)
        {
            _setter((TTarget)target, (TValue?)value);
        }
    }
}
