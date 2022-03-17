// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Editor
{
    [Shared]
    [ExportLanguageServiceFactory(typeof(VisualStudioCompletionBroker), RazorLanguage.Name, ServiceLayer.Default)]
    internal class DefaultVisualStudioCompletionBrokerFactory : ILanguageServiceFactory
    {
        private readonly ICompletionBroker _completionBroker;

        [ImportingConstructor]
        public DefaultVisualStudioCompletionBrokerFactory(ICompletionBroker completionBroker!!)
        {
            _completionBroker = completionBroker;
        }

        public ILanguageService CreateLanguageService(HostLanguageServices languageServices!!)
        {
            return new DefaultVisualStudioCompletionBroker(_completionBroker);
        }
    }
}
