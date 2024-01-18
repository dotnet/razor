// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;

namespace Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;

internal class TestProjectEngineFactoryProvider : IProjectEngineFactoryProvider
{
    public Action<RazorProjectEngineBuilder>? Configure { get; set; }

    [return: NotNullIfNotNull(nameof(fallbackFactory))]
    public IProjectEngineFactory? GetFactory(
        RazorConfiguration configuration,
        IProjectEngineFactory? fallbackFactory = null,
        bool requireSerializationSupport = false)
    {
        return new Factory(Configure);
    }

    private sealed class Factory(Action<RazorProjectEngineBuilder>? outerConfigure) : IProjectEngineFactory
    {
        public string ConfigurationName => "Test";
        public bool SupportsSerialization => false;

        public RazorProjectEngine Create(
            RazorConfiguration configuration,
            RazorProjectFileSystem fileSystem,
            Action<RazorProjectEngineBuilder>? innerConfigure)
        {
            return RazorProjectEngine.Create(configuration, fileSystem, b =>
            {
                innerConfigure?.Invoke(b);
                outerConfigure?.Invoke(b);
            });
        }
    }
}
