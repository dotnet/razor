// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.VisualStudio.Shell;

[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.Internal.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.Extensions.dll")]

[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.LanguageServer.Protocol", CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.dll", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "17.2.8.0", NewVersion = "17.2.8.0")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.LanguageServer.Protocol.Internal", CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.dll", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "17.2.8.0", NewVersion = "17.2.8.0")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.LanguageServer.Protocol.Extensions", CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.dll", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "17.2.8.0", NewVersion = "17.2.8.0")]
