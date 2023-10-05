// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorDiagnostic : IEquatable<RazorDiagnostic>, IFormattable
{
    private readonly RazorDiagnosticDescriptor _descriptor;
    private readonly object[] _args;

    public string Id => _descriptor.Id;
    public RazorDiagnosticSeverity Severity => _descriptor.Severity;
    public SourceSpan Span { get; }

    private RazorDiagnostic(RazorDiagnosticDescriptor descriptor, SourceSpan? span, object[]? args)
    {
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Span = span ?? SourceSpan.Undefined;
        _args = args ?? Array.Empty<object>();
    }

    public static RazorDiagnostic Create(RazorDiagnosticDescriptor descriptor)
        => new(descriptor, span: null, args: null);

    public static RazorDiagnostic Create(RazorDiagnosticDescriptor descriptor, SourceSpan? span)
        => new(descriptor, span, args: null);

    public static RazorDiagnostic Create(RazorDiagnosticDescriptor descriptor, params object[] args)
        => new(descriptor, span: null, args);

    public static RazorDiagnostic Create(RazorDiagnosticDescriptor descriptor, SourceSpan? span, params object[] args)
        => new(descriptor, span, args);

    public string GetMessage()
        => GetMessage(formatProvider: null);

    public string GetMessage(IFormatProvider? formatProvider)
        => string.Format(formatProvider, _descriptor.MessageFormat, _args);

    public override bool Equals(object? obj)
        => obj is RazorDiagnostic diagnostic &&
           Equals(diagnostic);

    public bool Equals(RazorDiagnostic? other)
    {
        if (other is null ||
            !_descriptor.Equals(other._descriptor) ||
            !Span.Equals(other.Span) ||
            _args.Length != other._args.Length)
        {
            return false;
        }

        for (var i = 0; i < _args.Length; i++)
        {
            if (!_args[i].Equals(other._args[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = HashCodeCombiner.Start();
        hash.Add(_descriptor.GetHashCode());
        hash.Add(Span.GetHashCode());

        for (var i = 0; i < _args.Length; i++)
        {
            hash.Add(_args[i]);
        }

        return hash;
    }

    public override string ToString()
    {
        return ((IFormattable)this).ToString(format: null, formatProvider: null);
    }

    string IFormattable.ToString(string format, System.IFormatProvider formatProvider)
    {
        // Our indices are 0-based, but we we want to print them as 1-based.
        return $"{Span.FilePath}({Span.LineIndex + 1},{Span.CharacterIndex + 1}): {Severity} {Id}: {GetMessage(formatProvider)}";
    }
}
