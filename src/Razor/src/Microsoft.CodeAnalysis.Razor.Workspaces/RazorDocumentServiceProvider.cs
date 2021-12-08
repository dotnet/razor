// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    internal class RazorDocumentServiceProvider : IRazorDocumentServiceProvider, IRazorDocumentOperationService
    {
        private readonly DynamicDocumentContainer? _documentContainer;
        private readonly object _lock;

        private IRazorSpanMappingService? _spanMappingService;
        private IRazorDocumentExcerptService? _documentExcerptService;
        private IRazorDocumentPropertiesService? _documentPropertiesService;

        public RazorDocumentServiceProvider()
            : this(null)
        {
        }

        public RazorDocumentServiceProvider(DynamicDocumentContainer? documentContainer)
        {
            _documentContainer = documentContainer;

            _lock = new object();
        }

        public bool CanApplyChange => false;

        public bool SupportDiagnostics => _documentContainer?.SupportsDiagnostics ?? false;

        public TService? GetService<TService>() where TService : class
        {
            if (_documentContainer is null)
            {
                return this as TService;
            }

            var serviceType = typeof(TService);

            if (serviceType == typeof(IRazorSpanMappingService))
            {
                if (_spanMappingService is null)
                {
                    lock (_lock)
                    {
                        if (_spanMappingService is null)
                        {
                            _spanMappingService = _documentContainer.GetMappingService();
                        }
                    }
                }

                return (TService)_spanMappingService;
            }

            if (serviceType == typeof(IRazorDocumentExcerptService))
            {
                if (_documentExcerptService is null)
                {
                    lock (_lock)
                    {
                        if (_documentExcerptService is null)
                        {
                            _documentExcerptService = _documentContainer.GetExcerptService();
                        }
                    }
                }

                return (TService)_documentExcerptService;
            }

            if (serviceType == typeof(IRazorDocumentPropertiesService))
            {
                if (_documentPropertiesService is null)
                {
                    lock (_lock)
                    {
                        if (_documentPropertiesService is null)
                        {
                            _documentPropertiesService = _documentContainer.GetDocumentPropertiesService();
                        }
                    }
                }

                return (TService)_documentPropertiesService;
            }

            return this as TService;
        }
    }
}
