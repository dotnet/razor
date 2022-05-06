// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell.Embeddable.Feedback;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Xunit.Harness;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    internal static class VisualStudioLogging
    {
        private static bool s_customLoggersAdded = false;

        public const string RazorOutputLogId = "RazorOutputLog";
        public const string LogHubLogId = "RazorLogHub";
        public const string ServiceHubLogId = "ServiceHubLog";
        public const string ComponentModelCacheId = "ComponentModelCache";

        private static readonly object s_lockObj = new();

        public static void AddCustomLoggers()
        {
            lock (s_lockObj)
            {
                // Add custom logs on failure if they haven't already been.
                if (!s_customLoggersAdded)
                {
                    DataCollectionService.RegisterCustomLogger(RazorOutputPaneLogger, RazorOutputLogId, "log");
                    DataCollectionService.RegisterCustomLogger(RazorLogHubLogger, LogHubLogId, "zip");
                    DataCollectionService.RegisterCustomLogger(RazorServiceHubLogger, ServiceHubLogId, "zip");
                    DataCollectionService.RegisterCustomLogger(RazorComponentModelCacheLogger, ComponentModelCacheId, "zip");

                    s_customLoggersAdded = true;
                }
            }
        }

        private static void RazorLogHubLogger(string filePath)
        {
            FeedbackLoggerInternal(filePath, "LogHub");
        }

        private static void RazorServiceHubLogger(string filePath)
        {
            FeedbackLoggerInternal(filePath, "ServiceHubLogs");
        }

        private static void RazorComponentModelCacheLogger(string filePath)
        {
            FeedbackLoggerInternal(filePath, "ComponentModelCache");
        }

        private static void FeedbackLoggerInternal(string filePath, string expectedFilePart)
        {
            var componentModel = GlobalServiceProvider.ServiceProvider.GetService<SComponentModel, IComponentModel>();
            if (componentModel is null)
            {
                // Unable to get componentModel
                return;
            }

            var feedbackFileProviders = componentModel.GetExtensions<IFeedbackDiagnosticFileProvider>();

            // Collect all the file names first since they can kick of file creation events that might need extra time to resolve.
            var files = new List<string>();
            foreach (var feedbackFileProvider in feedbackFileProviders)
            {
                files.AddRange(feedbackFileProvider.GetFiles());
            }

            _ = CollectFeedbackItemsAsync(files, filePath, expectedFilePart);
        }

        private static void RazorOutputPaneLogger(string filePath)
        {
            // JoinableTaskFactory.Run isn't an option because we might be disposing already.
            // Don't use ThreadHelper.JoinableTaskFactory in test methods, but it's correct here.
#pragma warning disable VSTHRD103 // Call async methods when in an async method
            ThreadHelper.JoinableTaskFactory.Run(async () =>
#pragma warning restore VSTHRD103 // Call async methods when in an async method
            {
                try
                {
                    var testServices = await Extensibility.Testing.TestServices.CreateAsync(ThreadHelper.JoinableTaskFactory);
                    var paneContent = await testServices.Output.GetRazorOutputPaneContentAsync(CancellationToken.None);
                    File.WriteAllText(filePath, paneContent);
                }
                catch (Exception)
                {
                    // Eat any errors so we don't block further collection
                }
            });
        }

        private static async Task CollectFeedbackItemsAsync(IEnumerable<string> files, string destination, string expectedFilePart)
        {
            // What's important in this weird threading stuff is ensuring we vacate the thread RazorLogHubLogger was called on
            // because if we don't it ends up blocking the thread that creates the zip file we need.
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);

                    // Only caputre loghub
                    if (name.Contains(expectedFilePart) && Path.GetExtension(file) == ".zip")
                    {
                        await Task.Run(() =>
                        {
                            WaitForFileExists(file);
                            if (File.Exists(file))
                            {
                                File.Copy(file, destination);
                            }
                        });
                    }
                }
            });
        }

        private static void WaitForFileExists(string file)
        {
            const int MaxRetries = 50;
            var retries = 0;
            while (!File.Exists(file) && retries < MaxRetries)
            {
                retries++;
                // Free your thread
                Thread.Yield();
                // Wait a bit
                Thread.Sleep(100);
            }
        }
    }
}
