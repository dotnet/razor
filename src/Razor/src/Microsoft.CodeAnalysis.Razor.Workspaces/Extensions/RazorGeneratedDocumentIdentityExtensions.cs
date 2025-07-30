// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.NET.Sdk.Razor.SourceGenerators;

namespace Microsoft.CodeAnalysis;

internal static class RazorGeneratedDocumentIdentityExtensions
{
    extension(RazorGeneratedDocumentIdentity identity)
    {
        public bool IsRazorSourceGeneratedDocument()
        {
            return identity.GeneratorTypeName == typeof(RazorSourceGenerator).FullName;
        }
    }
}
