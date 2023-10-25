// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.ProjectSystem;
using NoneItem = Microsoft.CodeAnalysis.Razor.ProjectSystem.ManagedProjectSystemSchema.NoneItem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

// This class is responsible for initializing the Razor ProjectSnapshotManager for cases where
// no Web or Razor SDK is used at all. ie, falling back even further than the fallback host, hence
// the name.
[AppliesTo("!DotNetCoreRazor & !DotNetCoreWeb & CSharp")]
[Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
internal class SuperFallbackWindowsRazorProjectHost : WindowsRazorProjectHostBase
{
    private static readonly ImmutableHashSet<string> s_ruleNames = ImmutableHashSet.CreateRange(new string[]
        {
            NoneItem.SchemaName,
            ConfigurationGeneralSchemaName,
        });
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;

    [ImportingConstructor]
    public SuperFallbackWindowsRazorProjectHost(
        IUnconfiguredProjectCommonServices commonServices,
        [Import(typeof(VisualStudioWorkspace))] Workspace workspace,
        ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
        ProjectConfigurationFilePathStore projectConfigurationFilePathStore,
        LanguageServerFeatureOptions languageServerFeatureOptions)
        : base(commonServices, workspace, projectSnapshotManagerDispatcher, projectConfigurationFilePathStore)
    {
        _languageServerFeatureOptions = languageServerFeatureOptions;
    }

    // Internal for testing
#pragma warning disable CS8618 // Non-nullable variable must contain a non-null value when exiting constructor. Consider declaring it as nullable.
    internal SuperFallbackWindowsRazorProjectHost(
#pragma warning restore CS8618 // Non-nullable variable must contain a non-null value when exiting constructor. Consider declaring it as nullable.
        IUnconfiguredProjectCommonServices commonServices,
        Workspace workspace,
        ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
        ProjectConfigurationFilePathStore projectConfigurationFilePathStore,
        ProjectSnapshotManagerBase projectManager)
        : base(commonServices, workspace, projectSnapshotManagerDispatcher, projectConfigurationFilePathStore, projectManager)
    {
    }

    protected override ImmutableHashSet<string> GetRuleNames() => s_ruleNames;

    protected override async Task HandleProjectChangeAsync(string sliceDimensions, IProjectVersionedValue<IProjectSubscriptionUpdate> update)
    {
        // If we don't know what to call the serialized project info class, then this class has no purpose
        if (_languageServerFeatureOptions is null)
        {
            return;
        }

        if (!TryGetIntermediateOutputPath(update.Value.CurrentState, out var intermediatePath))
        {
            // Can't find an IntermediateOutputPath, so don't know what to do with this project
            return;
        }

        // We need to deal with the case where the project was uninitialized, but now
        // is valid for Razor. In that case we might have previously seen all of the documents
        // but ignored them because the project wasn't active.
        //
        // So what we do to deal with this, is that we 'remove' all changed and removed items
        // and then we 'add' all current items. This allows minimal churn to the PSM, but still
        // makes us up-to-date.
        var documents = GetCurrentDocuments(update.Value);
        var changedDocuments = GetChangedAndRemovedDocuments(update.Value);

        if (documents.Length == 0 && changedDocuments.Length == 0)
        {
            // No Razor documents, we don't care about this project
            return;
        }

        await UpdateAsync(() =>
        {
            DefaultWindowsRazorProjectHost.TryGetRootNamespace(update.Value.CurrentState, out var rootNamespace);

            var hostProject = new HostProject(CommonServices.UnconfiguredProject.FullPath, intermediatePath, FallbackRazorConfiguration.Latest, rootNamespace, displayName: sliceDimensions);

            var projectConfigurationFile = Path.Combine(intermediatePath, _languageServerFeatureOptions.ProjectConfigurationFileName);
            ProjectConfigurationFilePathStore.Set(hostProject.Key, projectConfigurationFile);

            UpdateProjectUnsafe(hostProject);

            for (var i = 0; i < changedDocuments.Length; i++)
            {
                RemoveDocumentUnsafe(hostProject.Key, changedDocuments[i]);
            }

            for (var i = 0; i < documents.Length; i++)
            {
                AddDocumentUnsafe(hostProject.Key, documents[i]);
            }
        }, CancellationToken.None).ConfigureAwait(false);
    }

    // Internal for testing
    internal HostDocument[] GetCurrentDocuments(IProjectSubscriptionUpdate update)
    {
        using var _ = ListPool<HostDocument>.GetPooledObject(out var documents);

        if (update.CurrentState.TryGetValue(NoneItem.SchemaName, out var nonRule))
        {
            foreach (var (includePath, properties) in nonRule.Items)
            {
                if (TryGetRazorDocument(includePath, out var document))
                {
                    documents.Add(document);
                }
            }
        }

        return documents.ToArray();
    }

    // Internal for testing
    internal HostDocument[] GetChangedAndRemovedDocuments(IProjectSubscriptionUpdate update)
    {
        using var _ = ListPool<HostDocument>.GetPooledObject(out var documents);

        if (update.ProjectChanges.TryGetValue(NoneItem.SchemaName, out var nonRule))
        {
            foreach (var key in nonRule.Difference.RemovedItems.Concat(nonRule.Difference.ChangedItems))
            {
                if (nonRule.Before.Items.ContainsKey(key) &&
                    TryGetRazorDocument(key, out var document))
                {
                    documents.Add(document);
                }
            }
        }

        return documents.ToArray();
    }

    // Internal for testing
    internal bool TryGetRazorDocument(string includePath, [NotNullWhen(returnValue: true)] out HostDocument? razorDocument)
    {
        if (includePath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
            includePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = CommonServices.UnconfiguredProject.MakeRooted(includePath);
            var fileKind = FileKinds.GetFileKindFromFilePath(filePath);
            razorDocument = new HostDocument(filePath, includePath, fileKind);
            return true;
        }

        razorDocument = null;
        return false;
    }
}
