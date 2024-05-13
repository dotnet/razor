﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;

internal partial class RazorDiagnosticsPublisher
{
    private sealed class Comparer : IEqualityComparer<IDocumentSnapshot>
    {
        public static readonly Comparer Instance = new();

        private Comparer()
        {
        }

        public bool Equals(IDocumentSnapshot? x, IDocumentSnapshot? y)
        {
            var filePathX = x?.FilePath;
            var filePathY = y?.FilePath;

            return FilePathComparer.Instance.Equals(filePathX, filePathY);
        }

        public int GetHashCode(IDocumentSnapshot obj)
        {
            var filePath = obj.FilePath.AssumeNotNull();
            return FilePathComparer.Instance.GetHashCode(filePath);
        }
    }
}
