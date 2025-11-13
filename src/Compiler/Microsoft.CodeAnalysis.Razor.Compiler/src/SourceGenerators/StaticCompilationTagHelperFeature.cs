// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal sealed class StaticCompilationTagHelperFeature(Compilation compilation) : RazorEngineFeatureBase, ITagHelperFeature
    {
        private TagHelperDiscoveryService? _discoveryService;

        public TagHelperCollection GetTagHelpers(IAssemblySymbol targetAssembly, CancellationToken cancellationToken)
        {
            if (_discoveryService is null)
            {
                return [];
            }

            var discoveryResult = _discoveryService.GetTagHelpers(compilation, targetAssembly, cancellationToken);

            return discoveryResult.Collection;
        }

        TagHelperCollection ITagHelperFeature.GetTagHelpers(CancellationToken cancellationToken)
        {
            if (_discoveryService is null)
            {
                return [];
            }

            var discoveryResult = _discoveryService.GetTagHelpers(compilation, cancellationToken);

            return discoveryResult.Collection;
        }

        protected override void OnInitialized()
        {
            _discoveryService = Engine.GetFeatures<TagHelperDiscoveryService>().FirstOrDefault();
        }
    }
}
