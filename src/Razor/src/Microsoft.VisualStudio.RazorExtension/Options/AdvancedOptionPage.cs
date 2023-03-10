// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Options;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.RazorExtension.Options;

[Guid("8EBB7F64-5BF7-49E6-9023-7CD7B9912203")]
[ComVisible(true)]
internal class AdvancedOptionPage : DialogPage
{
    private readonly Lazy<OptionsStorage> _optionsStorage;

    public AdvancedOptionPage()
    {
        _optionsStorage = new Lazy<OptionsStorage>(() =>
        {
            var componentModel = (IComponentModel)Site.GetService(typeof(SComponentModel));
            Assumes.Present(componentModel);

            return componentModel.DefaultExportProvider.GetExportedValue<OptionsStorage>();
        });
    }

    [LocCategory(nameof(VSPackage.Formatting))]
    [LocDescription(nameof(VSPackage.Setting_FormattingOnTypeDescription))]
    [LocDisplayName(nameof(VSPackage.Setting_FormattingOnTypeDisplayName))]
    public bool FormatOnType
    {
        get => _optionsStorage.Value.FormatOnType;
        set => _optionsStorage.Value.FormatOnType = value;
    }
}
