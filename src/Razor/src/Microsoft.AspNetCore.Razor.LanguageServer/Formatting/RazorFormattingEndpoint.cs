// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class RazorFormattingEndpoint : IDocumentFormattingHandler, IDocumentRangeFormattingHandler
    {
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorFormattingService _razorFormattingService;
        private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor;
        private readonly ILogger _logger;

        public RazorFormattingEndpoint(
            SingleThreadedDispatcher singleThreadedDispatcher,
            DocumentResolver documentResolver,
            RazorFormattingService razorFormattingService,
            IOptionsMonitor<RazorLSPOptions> optionsMonitor,
            ILoggerFactory loggerFactory)
        {
            if (singleThreadedDispatcher is null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (razorFormattingService is null)
            {
                throw new ArgumentNullException(nameof(razorFormattingService));
            }

            if (optionsMonitor is null)
            {
                throw new ArgumentNullException(nameof(optionsMonitor));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
            _documentResolver = documentResolver;
            _razorFormattingService = razorFormattingService;
            _optionsMonitor = optionsMonitor;
            _logger = loggerFactory.CreateLogger<RazorFormattingEndpoint>();
        }

        DocumentRangeFormattingRegistrationOptions IRegistration<DocumentRangeFormattingRegistrationOptions>.GetRegistrationOptions()
        {
            return new DocumentRangeFormattingRegistrationOptions
            {
                DocumentSelector = RazorDefaults.Selector,
            };
        }

        DocumentFormattingRegistrationOptions IRegistration<DocumentFormattingRegistrationOptions>.GetRegistrationOptions()
        {
            return new DocumentFormattingRegistrationOptions
            {
                DocumentSelector = RazorDefaults.Selector,
            };
        }

        public async Task<TextEditContainer?> Handle(DocumentFormattingParams request, CancellationToken cancellationToken)
        {
            if (!_optionsMonitor.CurrentValue.EnableFormatting)
            {
                return null;
            }

            var document = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _singleThreadedDispatcher.DispatcherScheduler);

            if (document is null || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var span = TextSpan.FromBounds(0, codeDocument.Source.Length);
            var range = span.AsRange(codeDocument.GetSourceText());
            var edits = await _razorFormattingService.FormatAsync(request.TextDocument.Uri, document, range, request.Options, cancellationToken);

            var editContainer = new TextEditContainer(edits);
            return editContainer;
        }

#nullable disable // OmniSharp annotations don't allow a null return, though the spec does
        public async Task<TextEditContainer> Handle(DocumentRangeFormattingParams request, CancellationToken cancellationToken)
        {
            if (!_optionsMonitor.CurrentValue.EnableFormatting)
            {
                return null;
            }

            var document = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _singleThreadedDispatcher.DispatcherScheduler);

            if (document is null || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var edits = await _razorFormattingService.FormatAsync(request.TextDocument.Uri, document, request.Range, request.Options, cancellationToken);

            var editContainer = new TextEditContainer(edits);
            return editContainer;
        }
#nullable restore

        public void SetCapability(DocumentFormattingCapability capability)
        {
        }

        public void SetCapability(DocumentRangeFormattingCapability capability)
        {
        }
    }
}
