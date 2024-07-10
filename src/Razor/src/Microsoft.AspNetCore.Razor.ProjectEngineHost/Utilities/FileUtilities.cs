// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Globalization;
using System.IO;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal class FileUtilities
{
    /// <summary>
    /// Generate a file path with adjacent to our input path that has the
    /// correct file extension, using numbers to differentiate from
    /// any collisions.
    /// </summary>
    /// <param name="path">The origin file path.</param>
    /// <param name="extension">The desired file extension.</param>
    /// <returns>A non-existent file path with a name in the desired format and a corresponding extension.</returns>
    public static string GenerateUniquePath(string path, string extension)
    {
        var directoryName = Path.GetDirectoryName(path);
        var baseFileName = Path.GetFileNameWithoutExtension(path);

        var n = 0;
        string uniquePath;
        do
        {
            var identifier = n > 0 ? n.ToString(CultureInfo.InvariantCulture) : string.Empty;  // Make it look nice
            Assumes.NotNull(directoryName);

            uniquePath = Path.Combine(directoryName, $"{baseFileName}{identifier}{extension}");
            n++;
        }
        while (File.Exists(uniquePath));

        return uniquePath;
    }
}
