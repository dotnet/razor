// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

// Strictly speaking we don't need these aliases in this project, but keeping things consistent with other projects
// makes for a more pleasant development experience.

// Avoid extern alias in every file that needs to use Range
global using LspRange = Roslyn.LanguageServer.Protocol.Range;

// Avoid ambiguity errors because of our global using above
global using Range = System.Range;

// We put our extensions on Roslyn's LSP types in the same namespace, for convenience, but of course without the alias,
// so to prevent confusion at not needing a using directive to access types, but needing one for extensions, we just
// global using the our extensions (which of course means they didn't need to be in the same namespace for convenience!)
global using Roslyn.LanguageServer.Protocol;
