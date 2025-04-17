﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Shell;

[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.Internal.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.Extensions.dll")]

#if INCLUDE_ROSLYN_DEPS
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.CSharp", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.CSharp.Features", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.CSharp.Workspaces", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.EditorFeatures", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.EditorFeatures.Text", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.EditorFeatures.Wpf", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.ExternalAccess.Razor", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.InteractiveHost", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.Features", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.LanguageServer.Protocol", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.Remote.Workspaces", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.VisualBasic", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.VisualBasic.Features", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.VisualBasic.Workspaces", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.CodeAnalysis.Workspaces", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.LanguageServices", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.LanguageServices.Implementation", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.LanguageServices.CSharp", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]

[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.Workspaces.dll")]
#endif
