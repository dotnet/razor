// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public static class DirectoryHelper
    {
        public static IEnumerable<string> GetFilteredFiles(string workspaceDirectory, string searchPattern, IReadOnlyCollection<string> ignoredNames, ILogger? logger = null)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.GetFiles(workspaceDirectory, searchPattern, SearchOption.TopDirectoryOnly);
            }
            catch (PathTooLongException ex)
            {
                logger?.LogWarning(ex.Message);
                yield break;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(workspaceDirectory);
            }
            catch (PathTooLongException ex)
            {
                logger?.LogWarning(ex.Message);
                yield break;
            }

            foreach (var path in directories)
            {
                var directory = Path.GetDirectoryName(path);
                if (!ignoredNames.Contains(directory))
                {
                    foreach (var result in GetFilteredFiles(path, searchPattern, ignoredNames, logger))
                    {
                        yield return result;
                    }
                }
            }
        }
    }
}
