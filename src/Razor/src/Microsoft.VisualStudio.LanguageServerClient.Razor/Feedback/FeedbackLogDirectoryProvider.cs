﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Feedback
{
    internal abstract class FeedbackLogDirectoryProvider
    {
        public abstract bool DirectoryCreated { get; }

        public abstract string GetDirectory();
    }
}
