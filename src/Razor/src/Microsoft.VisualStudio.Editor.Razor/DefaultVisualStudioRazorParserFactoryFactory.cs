// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [Shared]
    [ExportLanguageServiceFactory(typeof(VisualStudioRazorParserFactory), RazorLanguage.Name, ServiceLayer.Default)]
    internal class DefaultVisualStudioRazorParserFactoryFactory : ILanguageServiceFactory
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public DefaultVisualStudioRazorParserFactoryFactory(
            ForegroundDispatcher foregroundDispatcher,
            JoinableTaskContext joinableTaskContext)
        {
            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _joinableTaskContext = joinableTaskContext;
        }
        public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
        {
            if (languageServices == null)
            {
                throw new ArgumentNullException(nameof(languageServices));
            }

            var workspaceServices = languageServices.WorkspaceServices;
            var errorReporter = workspaceServices.GetRequiredService<ErrorReporter>();
            var completionBroker = languageServices.GetRequiredService<VisualStudioCompletionBroker>();
            var projectEngineFactory = workspaceServices.GetRequiredService<ProjectSnapshotProjectEngineFactory>();

            return new DefaultVisualStudioRazorParserFactory(
                _foregroundDispatcher,
                _joinableTaskContext,
                errorReporter,
                completionBroker,
                projectEngineFactory);
        }
    }
}
