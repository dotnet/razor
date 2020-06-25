// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Razor
{
    internal static class UriExtensions
    {
        public static string GetAbsoluteOrUNCPath(this Uri uri)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            return uri.LocalPath;
        }
    }
}
