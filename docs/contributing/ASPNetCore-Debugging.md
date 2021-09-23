# Debugging with experimental ASP.NET Core bits
Sometimes it may be necessary to make changes in [`dotnet/aspnetcore`](https://github.com/dotnet/aspnetcore), and react to the changes in this repo. The following are steps which outline the general process in using ASP.NET Core development `nupkg`s with Razor Tooling.

## Steps:
1. Checkout [`dotnet/aspnetcore`](https://github.com/dotnet/aspnetcore), and follow the initialization instructions in the [Build From Source](https://github.com/dotnet/aspnetcore/blob/main/docs/BuildFromSource.md) guide.
2. `./restore.cmd`
3. Make the desired changes in `dotnet/aspnetcore`.
4. `./eng/build.cmd -pack`. The `-pack` option causes the creation of NuGet packages.
5. You should see the generated packages in the `aspnetcore\artifacts\packages\Debug\NonShipping` directory. The packages should end with `x.0.0-dev.nupkg` where `x` is the current .NET version.
6. Open `aspnetcore-tooling/NuGet.config` and add the local package sources:
- `<add key="ASPNETCORE_SHIPPING" value="<PATH_TO_ASPNET_CORE_REPO>\artifacts\packages\Debug\Shipping\" />`
- `<add key="ASPNETCORE_NONSHIPPING" value="<PATH_TO_ASPNET_CORE_REPO>\artifacts\packages\Debug\NonShipping\" />`

7. Open `aspnetcore-tooling/eng/Versions.props` and note the version for `MicrosoftCodeAnalysisRazorPackageVersion`. Ex. `5.0.0-rc.1.20380.7`.
8. Do a find in `Versions.props` for the version in step 7 and replace with `x.0.0-dev`.

## Notes:
- ⚠️ Ensure you do not commit the changes to `aspnetcore-tooling/NuGet.config` & `aspnetcore-tooling/eng/Versions.props`!
- If you're still seeing build errors after performing the above steps, you may have to temporarily modify `OldVersionUpperBound` and `NewVersion` of the first five assemblies in [AssemblyBindingRedirects.cs](https://github.com/dotnet/aspnetcore-tooling/blob/main/src/Razor/src/Microsoft.VisualStudio.RazorExtension/AssemblyBindingRedirects.cs) to match the assembly version of the aspnetcore packages above. You can find the assembly version by opening one of the packages with [ILSpy](https://github.com/icsharpcode/ILSpy/releases) or similar tool. 
- If you find the old packages are still being used after this change, purge the nuget cache here: `~\.nuget\packages`
