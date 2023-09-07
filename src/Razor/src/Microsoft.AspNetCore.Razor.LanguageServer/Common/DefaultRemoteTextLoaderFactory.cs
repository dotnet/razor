﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common;

internal class DefaultRemoteTextLoaderFactory : RemoteTextLoaderFactory
{
    public override TextLoader Create(string filePath)
    {
        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        var normalizedPath = FilePathNormalizer.Normalize(filePath);
        return new RemoteTextLoader(normalizedPath);
    }

    private class RemoteTextLoader : TextLoader
    {
        private readonly string _filePath;

        public RemoteTextLoader(string filePath)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            _filePath = filePath;
        }

        public override Task<TextAndVersion> LoadTextAndVersionAsync(LoadTextOptions options, CancellationToken cancellationToken)
        {

            TextAndVersion textAndVersion;

            try
            {
                var prevLastWriteTime = File.GetLastWriteTimeUtc(_filePath);

                using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var version = VersionStamp.Create(prevLastWriteTime);
                    var text = SourceText.From(stream);
                    textAndVersion = TextAndVersion.Create(text, version);
                }

                var newLastWriteTime = File.GetLastWriteTimeUtc(_filePath);
                if (!newLastWriteTime.Equals(prevLastWriteTime))
                {
                    throw new IOException(SR.FormatFile_Externally_Modified(_filePath));
                }
            }
            catch (IOException)
            {
                // This can typically occur when a file is renamed. What happens is the client "closes" the old file before any file system "rename" event makes it to us. Resulting
                // in us trying to refresh the "closed" files buffer with what's on disk; however, there's nothing actually on disk because the file was renamed.
                //
                // Can also occur when a file is in the middle of being copied resulting in a generic IO exception for the resource not being ready.
                textAndVersion = TextAndVersion.Create(SourceText.From(string.Empty), VersionStamp.Default, filePath: _filePath);
            }

            return Task.FromResult(textAndVersion);
        }
    }
}
