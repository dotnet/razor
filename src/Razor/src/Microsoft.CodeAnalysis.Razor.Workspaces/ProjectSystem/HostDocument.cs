﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal class HostDocument
    {
        public HostDocument(HostDocument other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            FileKind = other.FileKind;
            FilePath = other.FilePath;
            TargetPath = other.TargetPath;

            GeneratedDocumentContainer = new GeneratedDocumentContainer();
        }

        public HostDocument(string filePath, string targetPath)
            : this(filePath, targetPath, fileKind: null)
        {
        }

        public HostDocument(string filePath, string targetPath, string fileKind)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (targetPath is null)
            {
                throw new ArgumentNullException(nameof(targetPath));
            }

            FilePath = filePath;
            TargetPath = targetPath;
            FileKind = fileKind ?? FileKinds.GetFileKindFromFilePath(filePath);
            GeneratedDocumentContainer = new GeneratedDocumentContainer();
        }

        public string FileKind { get; }

        public string FilePath { get; }

        public string TargetPath { get; }

        public GeneratedDocumentContainer GeneratedDocumentContainer { get; }
    }
}
