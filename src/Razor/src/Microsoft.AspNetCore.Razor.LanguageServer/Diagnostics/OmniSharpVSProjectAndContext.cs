// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics
{
    internal class OmniSharpVSProjectAndContext
    {
        /// <summary>
        /// Gets or sets a human-readable identifier for the project in which the diagnostic was generated.
        /// </summary>
        public string? ProjectName { get; set; }

        /// <summary>
        /// Gets or sets A human-readable identifier for the build context (e.g. Win32 or MacOS)
        /// in which the diagnostic was generated.
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the project in which the diagnostic was generated.
        /// </summary>
        public string? ProjectIdentifier { get; set; }
    }
}
