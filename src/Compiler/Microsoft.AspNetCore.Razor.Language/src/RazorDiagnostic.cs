// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorDiagnostic : IEquatable<RazorDiagnostic>, IFormattable
{
    // Internal for testing
    internal RazorDiagnosticDescriptor Descriptor { get; }

    // Internal for testing
    internal object[] Args { get; }

    public string Id => Descriptor.Id;
    public RazorDiagnosticSeverity Severity => Descriptor.Severity;
    public SourceSpan Span { get; }

    private RazorDiagnostic(RazorDiagnosticDescriptor descriptor, SourceSpan? span, object[]? args)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Span = span ?? SourceSpan.Undefined;
        Args = args ?? Array.Empty<object>();
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
        => string.Format(formatProvider, Descriptor.MessageFormat, Args);

    public override bool Equals(object? obj)
        => obj is RazorDiagnostic diagnostic
            ? Equals(diagnostic)
            : false;

    public bool Equals(RazorDiagnostic other)
    {
        if (!Descriptor.Equals(other.Descriptor) ||
            !Span.Equals(other.Span) ||
            Args.Length != other.Args.Length)
        {
            return false;
        }

        for (var i = 0; i < Args.Length; i++)
        {
            if (!Args[i].Equals(other.Args[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = HashCodeCombiner.Start();
        hash.Add(Descriptor.GetHashCode());
        hash.Add(Span.GetHashCode());

        for (var i = 0; i < Args.Length; i++)
        {
            hash.Add(Args[i]);
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
