// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.LanguageServerClient.Razor.VS.LSClientRazor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Name(nameof(ViewCodeCommandHandler))]
    [Export(typeof(ICommandHandler))]
    [ContentType(RazorLSPConstants.RazorLSPContentTypeName)]
    internal sealed class ViewCodeCommandHandler : ICommandHandler<ViewCodeCommandArgs>
    {
        private const string CSharpFileExtension = ".cs";

        // Because query status happens all the time we want to cache the File.Exists checks for a reasonable amount of time
        private const int CacheTimeoutMilliseconds = 10000;
        private static readonly Stopwatch s_fileExistsStopwatch = Stopwatch.StartNew();
        private static readonly Dictionary<string, (bool exists, long addedMs)> s_fileExistsCache = new();

        private static readonly ImmutableHashSet<string> s_relatedRazorFileSuffixes = ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, new[] { ".cshtml", ".razor" });

        private static readonly CommandState s_availableCommandState = new CommandState(isAvailable: true, displayText: Resources.View_Code);

        public string DisplayName => nameof(ViewCodeCommandHandler);

        public CommandState GetCommandState(ViewCodeCommandArgs args)
        {
            if (TryGetCSharpFilePath(args.SubjectBuffer, out _))
            {
                return s_availableCommandState;
            }

            return CommandState.Unavailable;
        }

        public bool ExecuteCommand(ViewCodeCommandArgs args, CommandExecutionContext executionContext)
        {
            if (TryGetCSharpFilePath(args.SubjectBuffer, out var cSharpFilePath))
            {
                VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, cSharpFilePath);

                return true;
            }

            return false;
        }

        private static bool TryGetCSharpFilePath(ITextBuffer buffer, [NotNullWhen(true)] out string? codeFilePath)
        {
            codeFilePath = null;

            if (!buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document) ||
                document?.FilePath is null)
            {
                return false;
            }

            var filePath = document.FilePath;
            var extension = Path.GetExtension(filePath);

            if (!s_relatedRazorFileSuffixes.Contains(extension))
            {
                return false;
            }

            codeFilePath = Path.ChangeExtension(filePath, extension + CSharpFileExtension);

            var now = s_fileExistsStopwatch.ElapsedMilliseconds;

            if (!s_fileExistsCache.TryGetValue(codeFilePath, out var cache) ||
                now - cache.addedMs > CacheTimeoutMilliseconds)
            {
                var exists = File.Exists(codeFilePath);
                s_fileExistsCache[codeFilePath] = (exists, now);
            }

            return s_fileExistsCache[codeFilePath].exists;
        }
    }
}
