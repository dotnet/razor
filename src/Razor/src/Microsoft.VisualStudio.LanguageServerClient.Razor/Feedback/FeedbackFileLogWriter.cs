// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Feedback
{
    internal abstract class FeedbackFileLogWriter
    {
        public abstract void Write(string message);
    }
}
