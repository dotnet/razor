// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    internal class CachedTextLoader : TextLoader
    {
        private record TextAndVersionCache(DateTime LastModified, Task<TextAndVersion> TextAndVersionTask);

        private readonly string _filePath;

        private readonly TextLoader _baseLoader;

        private static readonly Dictionary<string, TextAndVersionCache> s_cache = new();

        private static readonly object s_loadLock = new();

        public CachedTextLoader(string filePath, TextLoader? baseLoader = null)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (baseLoader is null)
            {
                baseLoader = new FileTextLoader(filePath, defaultEncoding: null);
            }

            _filePath = filePath;
            _baseLoader = baseLoader;
        }

        public override Task<TextAndVersion> LoadTextAndVersionAsync(Workspace workspace, DocumentId documentId, CancellationToken cancellationToken)
        {
            var lastModified = GetLastWriteTimeUtc(_filePath);

            lock (s_loadLock)
            {
                Task<TextAndVersion> textAndVersionTask;
                if (s_cache.TryGetValue(_filePath, out var textAndVersionCache) &&
                    lastModified is not null &&
                    textAndVersionCache.LastModified == lastModified)
                {
                    textAndVersionTask = textAndVersionCache.TextAndVersionTask;
                }
                else
                {
                    textAndVersionTask = _baseLoader.LoadTextAndVersionAsync(workspace, documentId, cancellationToken);
                    s_cache[_filePath] = new TextAndVersionCache(lastModified ?? DateTime.UtcNow, textAndVersionTask);
                }

                return textAndVersionTask;
            }
        }

        internal virtual DateTime? GetLastWriteTimeUtc(string filePath)
        {
            var lastModified = File.GetLastWriteTimeUtc(filePath);

            if (lastModified.Year == 1601)
            {
                return null;
            }

            return lastModified;
        }
    }
}
