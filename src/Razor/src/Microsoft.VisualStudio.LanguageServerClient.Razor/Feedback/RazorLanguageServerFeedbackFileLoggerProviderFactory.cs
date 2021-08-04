// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Feedback
{
    [Shared]
    [Export(typeof(RazorLanguageServerFeedbackFileLoggerProviderFactory))]
    internal class RazorLanguageServerFeedbackFileLoggerProviderFactory : FeedbackFileLoggerProviderFactoryBase
    {
        [ImportingConstructor]
        public RazorLanguageServerFeedbackFileLoggerProviderFactory(FeedbackLogDirectoryProvider feedbackLogDirectoryProvider)
            : base(feedbackLogDirectoryProvider)
        {
        }
    }
}
