// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Common
{
    public class TestRazorDocumentServiceProvider : IRazorDocumentServiceProvider
    {
        public static readonly TestRazorDocumentServiceProvider Instance = new();

        public bool CanApplyChange => throw new NotImplementedException();

        public bool SupportDiagnostics => throw new NotImplementedException();

        TService IRazorDocumentServiceProvider.GetService<TService>()
        {
            var serviceType = typeof(TService);

            if (serviceType == typeof(IRazorSpanMappingService))
            {
                return (TService)(IRazorSpanMappingService)new TestRazorSpanMappingService();
            }

            if (serviceType == typeof(IRazorDocumentPropertiesService))
            {
                return (TService)(IRazorDocumentPropertiesService)new TestRazorDocumentPropertiesService();
            }

            return this as TService;
        }

        private class TestRazorSpanMappingService : IRazorSpanMappingService
        {
            public Task<ImmutableArray<RazorMappedSpanResult>> MapSpansAsync(
                Document document,
                IEnumerable<TextSpan> spans,
                CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        private class TestRazorDocumentPropertiesService : IRazorDocumentPropertiesService
        {
            public bool DesignTimeOnly => false;

            public string DiagnosticsLspClientName => "RazorCSharp";
        }
    }
}
