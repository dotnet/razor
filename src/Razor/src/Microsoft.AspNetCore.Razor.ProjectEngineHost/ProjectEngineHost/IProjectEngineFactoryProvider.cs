// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost;

internal interface IProjectEngineFactoryProvider
{
    [return: NotNullIfNotNull(nameof(fallbackFactory))]
    IProjectEngineFactory? GetFactory(
        RazorConfiguration configuration,
        IProjectEngineFactory? fallbackFactory = null,
        bool requireSerializationSupport = false);
}
