// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Editor;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [System.Composition.Shared]
    [Export(typeof(EditorSettingsManager))]
    internal class DefaultEditorSettingsManager : EditorSettingsManager
    {
        public override event EventHandler<EditorSettingsChangedEventArgs> Changed;

        private readonly object _settingsAccessorLock = new object();
        private EditorSettings _settings;

        [ImportingConstructor]
        public DefaultEditorSettingsManager([ImportMany] IEnumerable<EditorSettingsChangedTrigger> editorSettingsChangeTriggers)
        {
            _settings = EditorSettings.Default;

            foreach (var changeTrigger in editorSettingsChangeTriggers)
            {
                changeTrigger.Initialize(this);
            }
        }

        public override EditorSettings Current
        {
            get
            {
                lock (_settingsAccessorLock)
                {
                    return _settings;
                }
            }
        }

        public override void Update(EditorSettings updatedSettings!!)
        {
            lock (_settingsAccessorLock)
            {
                if (!_settings.Equals(updatedSettings))
                {
                    _settings = updatedSettings;
                    OnChanged();
                }
            }
        }

        private void OnChanged()
        {
            var args = new EditorSettingsChangedEventArgs(Current);
            Changed?.Invoke(this, args);
        }
    }
}
