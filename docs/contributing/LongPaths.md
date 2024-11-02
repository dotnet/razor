# Handling long paths on windows

This repository requires [windows long paths support] to be enabled.

## Git
In order to clone sucessfully, you might need to allow for [git long paths]:
```shell
git config --global core.longpaths true
```

## Building
While working with the project, you might encounter load- or buildtime-errors popping up, hinting on paths being too long on your local environment, f.e.:
```
error MSB4248: Cannot expand metadata in expression "$([MSBuild]::ValueOrDefault('%(FullPath)', '').StartsWith($([MSBuild]::EnsureTrailingSlash($(MSBuildProjectDirectory)))))".
The item metadata "%(FullPath)" cannot be applied to the path "TestFiles\IntegrationTests\ComponentDesignTimeCodeGenerationTest\GenericComponent_GenericEventCallbackWithGenericTypeParameter_NestedTypeInference\TestComponent.mappings.txt".
Path: {{REPOSITORY_ROOT}}\src\Compiler\Microsoft.AspNetCore.Razor.Language\test\TestFiles\IntegrationTests\ComponentDesignTimeCodeGenerationTest\GenericComponent_GenericEventCallbackWithGenericTypeParameter_NestedTypeInference\TestComponent.mappings.txt exceeds the OS max path limit. The fully qualified file name must be less than 260 characters.
```

or similar.

This repository also generates a warning when building on windows and long paths are not enabled:
```
error LongPathsDisabled: Long paths are required for this project. See 'docs/contributing/LongPaths.md' on how to overcome this.
```

To overcome this, apply one of the following options:

### Enable long path support
> :bulb: This is the preferred approach of the razor team

Either import / execute [/eng/enable-long-path.reg] or invoke the following powershell-script from an elevated prompt:

```ps1
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name "LongPathsEnabled" -Value "1" -PropertyType DWORD -Force
```

### Use [subst] to shorten your local path
> :warning: While this might work in your particular environment, chances are high that future changes might introduce even longer paths, which might then again result in above errors.
> Also, [subst] has no way to make a substitution permanent, so you might need to repeat this step after reboots, or ensure it get's executed at startup.
>
If you don't want to / can't enable long path support globally, try shortening your local path with [subst] where `R` is a free drive-letter, e.g.:

> ```ps1
> $dir = pwd
> subst R: $dir
> cd R:\
> ```

[git long paths]:https://stackoverflow.com/questions/22575662/filename-too-long-in-git-for-windows
[windows long paths support]:https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation?tabs=registry#enable-long-paths-in-windows-10-version-1607-and-later
[subst]:https://learn.microsoft.com/de-de/windows-server/administration/windows-commands/subst
[/eng/enable-long-path.reg]:../../eng/enable-long-paths.reg
