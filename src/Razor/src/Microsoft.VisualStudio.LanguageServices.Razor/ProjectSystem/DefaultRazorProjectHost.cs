﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Item = System.Collections.Generic.KeyValuePair<string, System.Collections.Immutable.IImmutableDictionary<string, string>>;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    // Somewhat similar to https://github.com/dotnet/project-system/blob/fa074d228dcff6dae9e48ce43dd4a3a5aa22e8f0/src/Microsoft.VisualStudio.ProjectSystem.Managed/ProjectSystem/LanguageServices/LanguageServiceHost.cs
    //
    // This class is responsible for intializing the Razor ProjectSnapshotManager for cases where
    // MSBuild provides configuration support (>= 2.1).
    [AppliesTo("DotNetCoreRazor & DotNetCoreRazorConfiguration")]
    [Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
    internal class DefaultRazorProjectHost : RazorProjectHostBase
    {
        private IDisposable _subscription;

        private const string RootNamespaceProperty = "RootNamespace";

        [ImportingConstructor]
        public DefaultRazorProjectHost(
            IUnconfiguredProjectCommonServices commonServices,
            [Import(typeof(VisualStudioWorkspace))] Workspace workspace,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            ProjectConfigurationFilePathStore projectConfigurationFilePathStore)
            : base(commonServices, workspace, projectSnapshotManagerDispatcher, projectConfigurationFilePathStore)
        {
        }

        // Internal for testing
        internal DefaultRazorProjectHost(
            IUnconfiguredProjectCommonServices commonServices,
            Workspace workspace,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            ProjectConfigurationFilePathStore projectConfigurationFilePathStore,
            ProjectSnapshotManagerBase projectManager)
            : base(commonServices, workspace, projectSnapshotManagerDispatcher, projectConfigurationFilePathStore, projectManager)
        {
        }

        protected override async Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            await base.InitializeCoreAsync(cancellationToken).ConfigureAwait(false);

            // Don't try to evaluate any properties here since the project is still loading and we require access
            // to the UI thread to push our updates.
            //
            // Just subscribe and handle the notification later.
            var receiver = new ActionBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>>(OnProjectChangedAsync);
            _subscription = CommonServices.ActiveConfiguredProjectSubscription.JointRuleSource.SourceBlock.LinkTo(
                receiver,
                initialDataAsNew: true,
                suppressVersionOnlyUpdates: true,
                ruleNames: new string[]
                {
                    Rules.RazorGeneral.SchemaName,
                    Rules.RazorConfiguration.SchemaName,
                    Rules.RazorExtension.SchemaName,
                    Rules.RazorComponentWithTargetPath.SchemaName,
                    Rules.RazorGenerateWithTargetPath.SchemaName,
                    ConfigurationGeneralSchemaName,
                });
        }

        protected override async Task DisposeCoreAsync(bool initialized)
        {
            await base.DisposeCoreAsync(initialized).ConfigureAwait(false);

            if (initialized && _subscription != null)
            {
                _subscription.Dispose();
            }
        }

        // Internal for testing
        internal async Task OnProjectChangedAsync(IProjectVersionedValue<IProjectSubscriptionUpdate> update)
        {
            if (IsDisposing || IsDisposed)
            {
                return;
            }

            await CommonServices.TasksService.LoadedProjectAsync(async () => await ExecuteWithLockAsync(async () =>
            {
                if (TryGetConfiguration(update.Value.CurrentState, out var configuration))
                {
                    TryGetRootNamespace(update.Value.CurrentState, out var rootNamespace);

                    // We need to deal with the case where the project was uninitialized, but now
                    // is valid for Razor. In that case we might have previously seen all of the documents
                    // but ignored them because the project wasn't active.
                    //
                    // So what we do to deal with this, is that we 'remove' all changed and removed items
                    // and then we 'add' all current items. This allows minimal churn to the PSM, but still
                    // makes us up to date.
                    var documents = GetCurrentDocuments(update.Value);
                    var changedDocuments = GetChangedAndRemovedDocuments(update.Value);

                    await UpdateAsync(() =>
                    {
                        var hostProject = new HostProject(CommonServices.UnconfiguredProject.FullPath, configuration, rootNamespace);

                        if (TryGetIntermediateOutputPath(update.Value.CurrentState, out var intermediatePath))
                        {
                            var projectRazorJson = Path.Combine(intermediatePath, "project.razor.json");
                            ProjectConfigurationFilePathStore.Set(hostProject.FilePath, projectRazorJson);
                        }

                        UpdateProjectUnsafe(hostProject);

                        for (var i = 0; i < changedDocuments.Length; i++)
                        {
                            RemoveDocumentUnsafe(changedDocuments[i]);
                        }

                        for (var i = 0; i < documents.Length; i++)
                        {
                            AddDocumentUnsafe(documents[i]);
                        }
                    }, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    // Ok we can't find a configuration. Let's assume this project isn't using Razor then.
                    await UpdateAsync(UninitializeProjectUnsafe, CancellationToken.None).ConfigureAwait(false);
                }
            }).ConfigureAwait(false), registerFaultHandler: true);
        }

        #region Configuration Helpers
        // Internal for testing
        internal static bool TryGetConfiguration(
            IImmutableDictionary<string, IProjectRuleSnapshot> state,
            out RazorConfiguration configuration)
        {
            if (!TryGetDefaultConfiguration(state, out var defaultConfiguration))
            {
                configuration = null;
                return false;
            }

            if (!TryGetLanguageVersion(state, out var languageVersion))
            {
                configuration = null;
                return false;
            }

            if (!TryGetConfigurationItem(defaultConfiguration, state, out var configurationItem))
            {
                configuration = null;
                return false;
            }

            var extensionNames = GetExtensionNames(configurationItem);
            if (!TryGetExtensions(extensionNames, state, out var extensions))
            {
                configuration = null;
                return false;
            }

            configuration = new ProjectSystemRazorConfiguration(languageVersion, configurationItem.Key, extensions);
            return true;
        }

        // Internal for testing
        internal static bool TryGetDefaultConfiguration(
            IImmutableDictionary<string, IProjectRuleSnapshot> state,
            out string defaultConfiguration)
        {
            if (!state.TryGetValue(Rules.RazorGeneral.SchemaName, out var rule))
            {
                defaultConfiguration = null;
                return false;
            }

            if (!rule.Properties.TryGetValue(Rules.RazorGeneral.RazorDefaultConfigurationProperty, out defaultConfiguration))
            {
                defaultConfiguration = null;
                return false;
            }

            if (string.IsNullOrEmpty(defaultConfiguration))
            {
                defaultConfiguration = null;
                return false;
            }

            return true;
        }

        // Internal for testing
        internal static bool TryGetLanguageVersion(
            IImmutableDictionary<string, IProjectRuleSnapshot> state,
            out RazorLanguageVersion languageVersion)
        {
            if (!state.TryGetValue(Rules.RazorGeneral.SchemaName, out var rule))
            {
                languageVersion = null;
                return false;
            }

            if (!rule.Properties.TryGetValue(Rules.RazorGeneral.RazorLangVersionProperty, out var languageVersionValue))
            {
                languageVersion = null;
                return false;
            }

            if (string.IsNullOrEmpty(languageVersionValue))
            {
                languageVersion = null;
                return false;
            }

            if (!RazorLanguageVersion.TryParse(languageVersionValue, out languageVersion))
            {
                languageVersion = RazorLanguageVersion.Latest;
            }

            return true;
        }

        // Internal for testing
        internal static bool TryGetConfigurationItem(
            string configuration,
            IImmutableDictionary<string, IProjectRuleSnapshot> state,
            out Item configurationItem)
        {
            if (!state.TryGetValue(Rules.RazorConfiguration.PrimaryDataSourceItemType, out var configurationState))
            {
                configurationItem = default;
                return false;
            }

            var items = configurationState.Items;
            foreach (var item in items)
            {
                if (item.Key == configuration)
                {
                    configurationItem = item;
                    return true;
                }
            }

            configurationItem = default;
            return false;
        }

        // Internal for testing
        internal static string[] GetExtensionNames(Item configurationItem)
        {
            // The list of extension names might not be present, because the configuration may not have any.
            configurationItem.Value.TryGetValue(Rules.RazorConfiguration.ExtensionsProperty, out var extensionNames);
            if (string.IsNullOrEmpty(extensionNames))
            {
                return Array.Empty<string>();
            }

            return extensionNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // Internal for testing
        internal static bool TryGetExtensions(
            string[] extensionNames,
            IImmutableDictionary<string, IProjectRuleSnapshot> state,
            out ProjectSystemRazorExtension[] extensions)
        {
            // The list of extensions might not be present, because the configuration may not have any.
            state.TryGetValue(Rules.RazorExtension.PrimaryDataSourceItemType, out var rule);

            var items = rule?.Items ?? ImmutableDictionary<string, IImmutableDictionary<string, string>>.Empty;
            var extensionList = new List<ProjectSystemRazorExtension>();
            foreach (var item in items)
            {
                var extensionName = item.Key;
                if (extensionNames.Contains(extensionName))
                {
                    extensionList.Add(new ProjectSystemRazorExtension(extensionName));
                }
            }

            extensions = extensionList.ToArray();
            return true;
        }

        // Internal for testing
        internal static bool TryGetRootNamespace(
            IImmutableDictionary<string, IProjectRuleSnapshot> state,
            out string rootNamespace)
        {
            if (!state.TryGetValue(ConfigurationGeneralSchemaName, out var rule))
            {
                rootNamespace = null;
                return false;
            }

            if (!rule.Properties.TryGetValue(RootNamespaceProperty, out var rootNamespaceValue))
            {
                rootNamespace = null;
                return false;
            }

            if (string.IsNullOrEmpty(rootNamespaceValue))
            {
                rootNamespace = null;
                return false;
            }

            rootNamespace = rootNamespaceValue;
            return true;
        }
        #endregion Configuration Helpers

        private HostDocument[] GetCurrentDocuments(IProjectSubscriptionUpdate update)
        {
            var documents = new List<HostDocument>();
            if (update.CurrentState.TryGetValue(Rules.RazorComponentWithTargetPath.SchemaName, out var rule))
            {
                foreach (var kvp in rule.Items)
                {
                    if (kvp.Value.TryGetValue(Rules.RazorComponentWithTargetPath.TargetPathProperty, out var targetPath) &&
                        !string.IsNullOrWhiteSpace(kvp.Key) &&
                        !string.IsNullOrWhiteSpace(targetPath))
                    {
                        var filePath = CommonServices.UnconfiguredProject.MakeRooted(kvp.Key);
                        var fileKind = FileKinds.GetComponentFileKindFromFilePath(filePath);

                        documents.Add(new HostDocument(filePath, targetPath, fileKind));
                    }
                }
            }

            if (update.CurrentState.TryGetValue(Rules.RazorGenerateWithTargetPath.SchemaName, out rule))
            {
                foreach (var kvp in rule.Items)
                {
                    if (kvp.Value.TryGetValue(Rules.RazorGenerateWithTargetPath.TargetPathProperty, out var targetPath) &&
                        !string.IsNullOrWhiteSpace(kvp.Key) &&
                        !string.IsNullOrWhiteSpace(targetPath))
                    {
                        var filePath = CommonServices.UnconfiguredProject.MakeRooted(kvp.Key);
                        documents.Add(new HostDocument(filePath, targetPath, FileKinds.Legacy));
                    }
                }
            }

            return documents.ToArray();
        }

        private HostDocument[] GetChangedAndRemovedDocuments(IProjectSubscriptionUpdate update)
        {
            var documents = new List<HostDocument>();
            if (update.ProjectChanges.TryGetValue(Rules.RazorComponentWithTargetPath.SchemaName, out var rule))
            {
                foreach (var key in rule.Difference.RemovedItems.Concat(rule.Difference.ChangedItems))
                {
                    if (rule.Before.Items.TryGetValue(key, out var value))
                    {
                        if (value.TryGetValue(Rules.RazorComponentWithTargetPath.TargetPathProperty, out var targetPath) &&
                            !string.IsNullOrWhiteSpace(key) &&
                            !string.IsNullOrWhiteSpace(targetPath))
                        {
                            var filePath = CommonServices.UnconfiguredProject.MakeRooted(key);
                            var fileKind = FileKinds.GetComponentFileKindFromFilePath(filePath);

                            documents.Add(new HostDocument(filePath, targetPath, fileKind));
                        }
                    }
                }
            }

            if (update.ProjectChanges.TryGetValue(Rules.RazorGenerateWithTargetPath.SchemaName, out rule))
            {
                foreach (var key in rule.Difference.RemovedItems.Concat(rule.Difference.ChangedItems))
                {
                    if (rule.Before.Items.TryGetValue(key, out var value))
                    {
                        if (value.TryGetValue(Rules.RazorGenerateWithTargetPath.TargetPathProperty, out var targetPath) &&
                            !string.IsNullOrWhiteSpace(key) &&
                            !string.IsNullOrWhiteSpace(targetPath))
                        {
                            var filePath = CommonServices.UnconfiguredProject.MakeRooted(key);
                            documents.Add(new HostDocument(filePath, targetPath, FileKinds.Legacy));
                        }
                    }
                }
            }

            return documents.ToArray();
        }
    }
}
