// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Settings;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Razor.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Options;

[Export(typeof(OptionsStorage))]
[Export(typeof(IAdvancedSettingsStorage))]
internal class OptionsStorage : IAdvancedSettingsStorage, IDisposable
{
    private readonly JoinableTask _initializeTask;
    private ImmutableArray<string> _taskListDescriptors = [];
    private ISettingsReader? _unifiedSettingsReader;
    private IDisposable? _unifiedSettingsSubscription;
    private bool _changedBeforeSubscription;

    public bool FormatOnType
    {
        set => SetBool(SettingsNames.FormatOnType.LegacyName, value);
        get => GetBool(SettingsNames.FormatOnType, defaultValue: true);
    }

    public bool AutoClosingTags
    {
        set => SetBool(SettingsNames.AutoClosingTags.LegacyName, value);
        get => GetBool(SettingsNames.AutoClosingTags, defaultValue: true);
    }

    public bool AutoInsertAttributeQuotes
    {
        set => SetBool(SettingsNames.AutoInsertAttributeQuotes.LegacyName, value);
        get => GetBool(SettingsNames.AutoInsertAttributeQuotes, defaultValue: true);
    }

    public bool ColorBackground
    {
        set => SetBool(SettingsNames.ColorBackground.LegacyName, value);
        get => GetBool(SettingsNames.ColorBackground, defaultValue: false);
    }

    public bool CodeBlockBraceOnNextLine
    {
        set => SetBool(SettingsNames.CodeBlockBraceOnNextLine.LegacyName, value);
        get => GetBool(SettingsNames.CodeBlockBraceOnNextLine, defaultValue: false);
    }

    public bool CommitElementsWithSpace
    {
        set => SetBool(SettingsNames.CommitElementsWithSpace.LegacyName, value);
        get => GetBool(SettingsNames.CommitElementsWithSpace, defaultValue: true);
    }

    public SnippetSetting Snippets
    {
        set => SetInt(SettingsNames.Snippets.LegacyName, (int)value);
        get => GetEnum(SettingsNames.Snippets, SnippetSetting.All);
    }

    public LogLevel LogLevel
    {
        set => SetInt(SettingsNames.LogLevel.LegacyName, (int)value);
        get => GetEnum(SettingsNames.LogLevel, LogLevel.Warning);
    }

    public bool FormatOnPaste
    {
        set => SetBool(SettingsNames.FormatOnPaste.LegacyName, value);
        get => GetBool(SettingsNames.FormatOnPaste, defaultValue: true);
    }

    public ImmutableArray<string> TaskListDescriptors
    {
        get { return _taskListDescriptors; }
    }

    [ImportingConstructor]
    public OptionsStorage(
        SVsServiceProvider synchronousServiceProvider,
        [Import(typeof(SAsyncServiceProvider))] IAsyncServiceProvider serviceProvider,
        Lazy<ITelemetryReporter> telemetryReporter,
        JoinableTaskContext joinableTaskContext)
    {
        _initializeTask = joinableTaskContext.Factory.RunAsync(async () =>
        {
            var unifiedSettingsManager = await serviceProvider.GetServiceAsync<SVsUnifiedSettingsManager, ISettingsManager>();
            _unifiedSettingsReader = unifiedSettingsManager.GetReader();
            _unifiedSettingsSubscription = _unifiedSettingsReader.SubscribeToChanges(OnUnifiedSettingsChanged, SettingsNames.AllSettings);

            await GetTaskListDescriptorsAsync(joinableTaskContext.Factory, serviceProvider);
        });

        // NotifyChange waits for the initialize task to be finished, but we still want to notify once we've
        // done loading, so do it in a background continuation.
        _initializeTask.Task.ContinueWith(t =>
        {
            NotifyChange();
        }, TaskScheduler.Default).Forget();
    }

    private async Task GetTaskListDescriptorsAsync(JoinableTaskFactory jtf, IAsyncServiceProvider serviceProvider)
    {
        await jtf.SwitchToMainThreadAsync();

        var taskListService = await serviceProvider.GetServiceAsync<IVsTaskList, IVsCommentTaskInfo>();
        if (taskListService is null)
        {
            return;
        }

        // Not sure why, but the VS Threading analyzer isn't recognizing that we switched to the main thread, above.
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
        ErrorHandler.ThrowOnFailure(taskListService.TokenCount(out var count));
        var tokens = new IVsCommentTaskToken[count];
        ErrorHandler.ThrowOnFailure(taskListService.EnumTokens(out var enumerator));
        ErrorHandler.ThrowOnFailure(enumerator.Next((uint)count, tokens, out var numFetched));

        using var tokensBuilder = new PooledArrayBuilder<string>(capacity: (int)numFetched);
        for (var i = 0; i < numFetched; i++)
        {
            tokens[i].Text(out var text);
            tokensBuilder.Add(text);
        }
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

        _taskListDescriptors = tokensBuilder.ToImmutable();
    }

    public async Task OnChangedAsync(Action<ClientAdvancedSettings> changed)
    {
        await _initializeTask.JoinAsync();

        _changed += (_, args) => changed(args.Settings);

        // Since initialize happens async, we don't want our subscribers to miss the initial update, so trigger it now, since we know
        // initialization is done.
        if (_changedBeforeSubscription)
        {
            changed(GetAdvancedSettings());
        }
    }

    private EventHandler<ClientAdvancedSettingsChangedEventArgs>? _changed;

    public ClientAdvancedSettings GetAdvancedSettings()
        => new(FormatOnType, AutoClosingTags, AutoInsertAttributeQuotes, ColorBackground, CodeBlockBraceOnNextLine, CommitElementsWithSpace, Snippets, LogLevel, FormatOnPaste, TaskListDescriptors);

    public bool GetBool(string name, bool defaultValue)
    {
        if (_unifiedSettingsReader.AssumeNotNull().GetValue<bool>(name) is { Outcome: SettingRetrievalOutcome.Success, Value: { } unifiedValue })
        {
            return unifiedValue;
        }

        return defaultValue;
    }

    public void SetBool(string name, bool value)
    {
        _writableSettingsStore.SetBoolean(SettingsNames.LegacyCollection, name, value);
        _telemetryReporter.Value.ReportEvent("OptionChanged", Severity.Normal, new Property(name, value));

        NotifyChange();
    }

    public T GetEnum<T>(string name, T defaultValue) where T : struct, Enum
    {
        if (_unifiedSettingsReader.AssumeNotNull().GetValue<string>(name) is { Outcome: SettingRetrievalOutcome.Success, Value: { } unifiedValue })
        {
            if (Enum.TryParse<T>(unifiedValue, ignoreCase: true, out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    public void SetInt(string name, int value)
    {
        _writableSettingsStore.SetInt32(SettingsNames.LegacyCollection, name, value);
        _telemetryReporter.Value.ReportEvent("OptionChanged", Severity.Normal, new Property(name, value));

        NotifyChange();
    }

    private void NotifyChange()
    {
        _initializeTask.Join();

        if (_changed is null)
        {
            _changedBeforeSubscription = true;
        }
        else
        {
            _changed?.Invoke(this, new ClientAdvancedSettingsChangedEventArgs(GetAdvancedSettings()));
        }
    }

    private void OnUnifiedSettingsChanged(SettingsUpdate update)
    {
        NotifyChange();
    }

    public void Dispose()
    {
        _unifiedSettingsSubscription?.Dispose();
    }
}
