// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal static class DirectoryHelper
    {
        /// <summary>
        /// Finds all the files in  a directory which meet the given criteria.
        /// </summary>
        /// <param name="workspaceDirectory">The directory to be searched.</param>
        /// <param name="searchPattern">The pattern to apply when searching.</param>
        /// <param name="ignoredDirectories">List of directories to skip when recursing.</param>
        /// <param name="logger">An optional logger to report on exceptional situations such as <see cref="PathTooLongException"/>.</param>
        /// <returns>A list of files within the given directory that meet the search criteria.</returns>
        /// <remarks>This method is needed to avoid problematic folders such as "node_modules" which are known not to yield the desired results or may cause performance issues.</remarks>
        internal static IEnumerable<string> GetFilteredFiles(string workspaceDirectory, string searchPattern, IReadOnlyCollection<string> ignoredDirectories, ILogger? logger = null)
        {
            if (workspaceDirectory is null)
            {
                throw new ArgumentNullException(nameof(workspaceDirectory));
            }

            if (searchPattern is null)
            {
                throw new ArgumentNullException(nameof(searchPattern));
            }

            if (ignoredDirectories is null || ignoredDirectories.Count == 0)
            {
                throw new ArgumentNullException(nameof(ignoredDirectories));
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(workspaceDirectory, searchPattern, SearchOption.TopDirectoryOnly);
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

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(workspaceDirectory);
            }
            catch (PathTooLongException ex)
            {
                logger?.LogWarning(ex.Message);
                yield break;
            }

            foreach (var path in directories)
            {
                var directory = Path.GetDirectoryName(path);
                if (!ignoredDirectories.Contains(directory, StringComparer.Ordinal))
                {
                    foreach (var result in GetFilteredFiles(path, searchPattern, ignoredDirectories, logger))
                    {
                        yield return result;
                    }
                }
            }
        }
    }
}
