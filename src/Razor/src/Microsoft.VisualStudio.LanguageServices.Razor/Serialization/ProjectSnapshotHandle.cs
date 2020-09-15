// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    [DataContract]
    internal sealed class ProjectSnapshotHandle
    {
        [DataMember(Order = 0)]
        public string FilePath { get; }

        [DataMember(Order = 1)]
        public RazorConfiguration Configuration { get; }

        [DataMember(Order = 2)]
        public string RootNamespace { get; }

        public ProjectSnapshotHandle(
            string filePath,
            RazorConfiguration configuration,
            string rootNamespace)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
            Configuration = configuration;
            RootNamespace = rootNamespace;
        }
    }
}
