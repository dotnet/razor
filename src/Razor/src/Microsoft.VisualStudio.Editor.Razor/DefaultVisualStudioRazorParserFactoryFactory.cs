// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor;

[Shared]
[ExportLanguageServiceFactory(typeof(VisualStudioRazorParserFactory), RazorLanguage.Name, ServiceLayer.Default)]
internal class DefaultVisualStudioRazorParserFactoryFactory : ILanguageServiceFactory
{
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly IErrorReporter _errorReporter;

    [ImportingConstructor]
    public DefaultVisualStudioRazorParserFactoryFactory(JoinableTaskContext joinableTaskContext, IErrorReporter errorReporter)
    {
        _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
    }

    public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
    {
        if (languageServices is null)
        {
            throw new ArgumentNullException(nameof(languageServices));
        }

        var workspaceServices = languageServices.WorkspaceServices;
        var completionBroker = languageServices.GetRequiredService<VisualStudioCompletionBroker>();
        var projectEngineFactory = workspaceServices.GetRequiredService<ProjectSnapshotProjectEngineFactory>();

        return new DefaultVisualStudioRazorParserFactory(
            _joinableTaskContext,
            _errorReporter,
            completionBroker,
            projectEngineFactory);
    }
}
