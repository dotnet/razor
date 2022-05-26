// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert
{
    internal class OnAutoInsertParams : VSInternalDocumentOnAutoInsertParams, IRequest<VSInternalDocumentOnAutoInsertResponseItem>
    {
    }
}
