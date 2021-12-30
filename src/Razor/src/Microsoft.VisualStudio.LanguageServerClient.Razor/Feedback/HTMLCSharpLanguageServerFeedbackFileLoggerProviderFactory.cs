// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Composition;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Feedback
{
    [Shared]
    [Export(typeof(HTMLCSharpLanguageServerFeedbackFileLoggerProviderFactory))]
    internal class HTMLCSharpLanguageServerFeedbackFileLoggerProviderFactory : FeedbackFileLoggerProviderFactoryBase
    {
        [ImportingConstructor]
        public HTMLCSharpLanguageServerFeedbackFileLoggerProviderFactory(FeedbackLogDirectoryProvider feedbackLogDirectoryProvider)
            : base(feedbackLogDirectoryProvider)
        {
        }
    }
}
