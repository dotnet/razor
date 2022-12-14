﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage.MessageInterception;

/// <summary>
/// Specifies what LSP method the <see cref="MessageInterceptor"/> handles. May be applied multiple times.
/// </summary>
[ExcludeFromCodeCoverage]
internal class InterceptMethodAttribute : MultipleBaseMetadataAttribute
{
    public InterceptMethodAttribute(string interceptMethods)
    {
        InterceptMethods = interceptMethods;
    }

    // name must be kept in sync with IInterceptMethodMetadata
    public string InterceptMethods { get; }
}
