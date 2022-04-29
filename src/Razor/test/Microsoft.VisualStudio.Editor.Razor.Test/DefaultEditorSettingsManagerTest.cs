// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Xunit;

namespace Microsoft.VisualStudio.Editor.Razor
{
    public class DefaultEditorSettingsManagerTest : ProjectSnapshotManagerDispatcherTestBase
    {
        private IEnumerable<EditorSettingsChangedTrigger> EditorSettingsChangeTriggers => Array.Empty<EditorSettingsChangedTrigger>();

        [Fact]
        public void ChangeTriggersGetInitialized()
        {
            // Act
            var triggers = new TestChangeTrigger[]
            {
                new TestChangeTrigger(),
                new TestChangeTrigger(),
            };
            var manager = new DefaultEditorSettingsManager(triggers);

            // Assert
            Assert.All(triggers, (trigger) => Assert.True(trigger.Initialized));
        }

        [Fact]
        public void InitialSettingsAreDefault()
        {
            // Act
            var manager = new DefaultEditorSettingsManager(EditorSettingsChangeTriggers);

            // Assert
            Assert.Equal(EditorSettings.Default, manager.Current);
        }

        [Fact]
        public void Update_TriggersChangedIfEditorSettingsAreDifferent()
        {
            // Arrange
            var manager = new DefaultEditorSettingsManager(EditorSettingsChangeTriggers);
            var called = false;
            manager.Changed += (caller, args) => called = true;
            var settings = new EditorSettings(indentWithTabs: true, indentSize: 7);

            // Act
            manager.Update(settings);

            // Assert
            Assert.True(called);
            Assert.Equal(settings, manager.Current);
        }

        [Fact]
        public void Update_DoesNotTriggerChangedIfEditorSettingsAreSame()
        {
            // Arrange
            var manager = new DefaultEditorSettingsManager(EditorSettingsChangeTriggers);
            var called = false;
            manager.Changed += (caller, args) => called = true;
            var originalSettings = manager.Current;

            // Act
            manager.Update(EditorSettings.Default);

            // Assert
            Assert.False(called);
            Assert.Same(originalSettings, manager.Current);
        }

        private class TestChangeTrigger : EditorSettingsChangedTrigger
        {
            public bool Initialized { get; private set; }

            public override void Initialize(EditorSettingsManager editorSettingsManager)
            {
                Initialized = true;
            }
        }
    }
}
