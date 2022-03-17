﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Shared]
    [Export(typeof(ProjectConfigurationFilePathStore))]
    internal class DefaultProjectConfigurationFilePathStore : ProjectConfigurationFilePathStore
    {
        private readonly Dictionary<string, string> _mappings;
        private readonly object _mappingsLock;

        public override event EventHandler<ProjectConfigurationFilePathChangedEventArgs>? Changed;

        [ImportingConstructor]
        public DefaultProjectConfigurationFilePathStore()
        {
            _mappings = new Dictionary<string, string>(FilePathComparer.Instance);
            _mappingsLock = new object();
        }

        public override IReadOnlyDictionary<string, string> GetMappings() => new Dictionary<string, string>(_mappings);

        public override void Set(string projectFilePath!!, string configurationFilePath!!)
        {
            lock (_mappingsLock)
            {
                // Resolve any relative pathing in the configuration path so we can talk in absolutes
                configurationFilePath = Path.GetFullPath(configurationFilePath);

                if (_mappings.TryGetValue(projectFilePath, out var existingConfigurationFilePath) &&
                    FilePathComparer.Instance.Equals(configurationFilePath, existingConfigurationFilePath))
                {
                    // Already have this mapping, don't invoke changed.
                    return;
                }

                _mappings[projectFilePath] = configurationFilePath;
            }

            var args = new ProjectConfigurationFilePathChangedEventArgs(projectFilePath, configurationFilePath);
            Changed?.Invoke(this, args);
        }

        public override void Remove(string projectFilePath!!)
        {
            lock (_mappingsLock)
            {
                if (!_mappings.Remove(projectFilePath))
                {
                    // We weren't tracking the project file path, no-op.
                    return;
                }
            }

            var args = new ProjectConfigurationFilePathChangedEventArgs(projectFilePath, configurationFilePath: null);
            Changed?.Invoke(this, args);
        }

        public override bool TryGet(string projectFilePath!!, out string configurationFilePath)
        {
            lock (_mappingsLock)
            {
                return _mappings.TryGetValue(projectFilePath, out configurationFilePath);
            }
        }
    }
}
