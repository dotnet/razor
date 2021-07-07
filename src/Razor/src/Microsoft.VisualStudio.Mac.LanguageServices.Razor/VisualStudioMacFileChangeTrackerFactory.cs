// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor.Documents;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor
{
    internal class VisualStudioMacFileChangeTrackerFactory : FileChangeTrackerFactory
    {
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;

        public VisualStudioMacFileChangeTrackerFactory(SingleThreadedDispatcher singleThreadedDispatcher)
        {
            if (singleThreadedDispatcher == null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
        }

        public override FileChangeTracker Create(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException(Resources.ArgumentCannotBeNullOrEmpty, nameof(filePath));
            }

            var fileChangeTracker = new VisualStudioMacFileChangeTracker(filePath, _singleThreadedDispatcher);
            return fileChangeTracker;
        }
    }
}
