﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Remote.Razor.Completion;

[Export(typeof(CompletionTriggerAndCommitCharacters)), Shared]
[method: ImportingConstructor]
internal sealed class OOPCompletionTriggerAndCommitCharacters(LanguageServerFeatureOptions options)
    : CompletionTriggerAndCommitCharacters(options);
