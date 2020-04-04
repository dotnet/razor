using Microsoft.VisualStudio.Shell;

[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.VisualStudio.LanguageServer.Protocol.Extensions.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\OmniSharp.Extensions.JsonRpc.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\OmniSharp.Extensions.LanguageProtocol.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\OmniSharp.Extensions.LanguageServer.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\MediatR.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\MediatR.Extensions.Microsoft.DependencyInjection.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.Options.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.DependencyInjection.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.DependencyInjection.Abstractions.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.Configuration.Json.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.Configuration.FileExtensions.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.Configuration.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.Configuration.Abstractions.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.Logging.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.Logging.Abstractions.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.Primitives.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.FileSystemGlobbing.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.FileProviders.Abstractions.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Extensions.FileProviders.Physical.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\System.Reactive.dll")]
[assembly: ProvideBindingRedirection(
    AssemblyName = "MediatR",
    PublicKeyToken = "bb9a41a5e8aaa7e2",
    OldVersionLowerBound = "8.0.0.0",
    OldVersionUpperBound = "8.0.1.0",
    NewVersion = "8.0.1.0")]
[assembly: ProvideBindingRedirection(
    AssemblyName = "Microsoft.Extensions.Configuration.Abstractions",
    PublicKeyToken = "adb9793829ddae60",
    OldVersionLowerBound = "0.0.0.0",
    OldVersionUpperBound = "5.0.0.0",
    NewVersion = "5.0.0.0")]
[assembly: ProvideBindingRedirection(
    AssemblyName = "Microsoft.Extensions.Configuration",
    PublicKeyToken = "adb9793829ddae60",
    OldVersionLowerBound = "0.0.0.0",
    OldVersionUpperBound = "5.0.0.0",
    NewVersion = "5.0.0.0")]
[assembly: ProvideBindingRedirection(
    AssemblyName = "Microsoft.Extensions.DependencyInjection.Abstractions",
    PublicKeyToken = "adb9793829ddae60",
    OldVersionLowerBound = "2.0.0.0",
    OldVersionUpperBound = "3.1.0.0",
    NewVersion = "3.1.0.0")]
[assembly: ProvideBindingRedirection(
    AssemblyName = "Microsoft.Extensions.Primitives",
    PublicKeyToken = "adb9793829ddae60",
    OldVersionLowerBound = "0.0.0.0",
    OldVersionUpperBound = "5.0.0.0",
    NewVersion = "5.0.0.0")]