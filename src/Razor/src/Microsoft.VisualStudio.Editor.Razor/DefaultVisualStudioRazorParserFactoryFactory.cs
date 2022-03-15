// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

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
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public DefaultVisualStudioRazorParserFactoryFactory(JoinableTaskContext joinableTaskContext!!)
        {
            _joinableTaskContext = joinableTaskContext;
        }
        public ILanguageService CreateLanguageService(HostLanguageServices languageServices!!)
        {
            var workspaceServices = languageServices.WorkspaceServices;
            var errorReporter = workspaceServices.GetRequiredService<ErrorReporter>();
            var completionBroker = languageServices.GetRequiredService<VisualStudioCompletionBroker>();
            var projectEngineFactory = workspaceServices.GetRequiredService<ProjectSnapshotProjectEngineFactory>();

            return new DefaultVisualStudioRazorParserFactory(
                _joinableTaskContext,
                errorReporter,
                completionBroker,
                projectEngineFactory);
        }
    }
}
