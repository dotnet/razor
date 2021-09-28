// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class GeneratedDocumentContainerStore : ProjectSnapshotChangeTrigger
    {
        public abstract ReferenceOutputCapturingContainer Get(string physicalFilePath);
    }
}
