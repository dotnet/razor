// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Shell;

[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.Internal.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.Extensions.dll")]

[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.LanguageServer.Protocol", GenerateCodeBase = true, OldVersionLowerBound = "17.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.LanguageServer.Protocol.Internal", GenerateCodeBase = true, OldVersionLowerBound = "17.4.0.0", OldVersionUpperBound = "Current")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.LanguageServer.Protocol.Extensions", GenerateCodeBase = true, OldVersionLowerBound = "17.4.0.0", OldVersionUpperBound = "Current")]

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
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.LanguageServices.Xaml", GenerateCodeBase = true, OldVersionLowerBound = "4.4.0.0", OldVersionUpperBound = "Current")]

[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.VisualStudio.Threading", GenerateCodeBase = true, OldVersionLowerBound = "17.4.0.0", OldVersionUpperBound = "Current")]

[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.CSharp.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.CSharp.Features.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.CSharp.Workspaces.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.EditorFeatures.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.EditorFeatures.Text.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.EditorFeatures.Wpf.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.ExternalAccess.Razor.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.InteractiveHost.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.Features.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.LanguageServer.Protocol.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.Remote.Workspaces.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.VisualBasic.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.VisualBasic.Features.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.VisualBasic.Workspaces.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.CodeAnalysis.Workspaces.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServices.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServices.Implementation.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServices.CSharp.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServices.Xaml.dll")]
#endif
