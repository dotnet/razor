// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.Editor.Razor;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.LegacyEditor;

/// <summary>
///  This class creates and caches wrappers for various Razor objects without directly exposing
///  Razor types.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static partial class RazorWrapperFactory
{
    private static readonly ConditionalWeakTable<object, object> s_objectToWrapperMap = new();

    [return: NotNullIfNotNull(nameof(obj))]
    private static TResult? GetOrCreateWrapper<TInner, TWrapper, TResult>(object? obj, Func<TInner, TWrapper> createWrapper)
        where TInner : class
        where TResult : class
        where TWrapper : class, TResult
    {
        var inner = CastObject<TInner>(obj);
        if (inner is null)
        {
            return null;
        }

        if (s_objectToWrapperMap.TryGetValue(inner, out var wrapperObj))
        {
            return (TResult)wrapperObj;
        }

        var wrapper = createWrapper(inner);
        s_objectToWrapperMap.Add(inner, wrapper);

        return wrapper;
    }

    internal static IRazorParser GetOrCreateParser(object obj)
        => GetOrCreateWrapper<VisualStudioRazorParser, ParserWrapper, IRazorParser>(obj, parser => new ParserWrapper(parser));

    internal static RazorSourceSpan ConvertSourceSpan(object obj)
    {
        var span = CastStruct<SourceSpan>(obj);

        return ConvertSourceSpan(span);
    }

    [return: NotNullIfNotNull(nameof(obj))]
    internal static RazorSourceChange? ConvertSourceChange(object? obj)
    {
        return CastObject<SourceChange>(obj) is SourceChange change
            ? new RazorSourceChange(
                Span: ConvertSourceSpan(change.Span),
                NewText: change.NewText)
            : null;
    }

    private static RazorSourceSpan ConvertSourceSpan(SourceSpan span)
        => new(
            FilePath: span.FilePath,
            AbsoluteIndex: span.AbsoluteIndex,
            LineIndex: span.LineIndex,
            CharacterIndex: span.CharacterIndex,
            Length: span.Length,
            LineCount: span.LineCount,
            EndCharacterIndex: span.EndCharacterIndex);

    [return: NotNullIfNotNull(nameof(obj))]
    private static T? CastObject<T>(object? obj)
        where T : class
    {
        return obj switch
        {
            T result => result,
            null => null,
            _ => throw new ArgumentException($"Expected {typeof(T).FullName}.", nameof(obj))
        };
    }

    private static T CastStruct<T>(object obj)
        where T : struct
    {
        return obj is T result
            ? result
            : throw new ArgumentException($"Expected {typeof(T).FullName}.", nameof(obj));
    }
}
