// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.ProjectSystem;

[Shared]
[Export(typeof(RazorProjectInfoEndpointPublisher))]
internal class RazorProjectInfoEndpointPublisher
{
    private readonly LSPRequestInvoker _requestInvoker;
    private bool _useCache;

    private readonly Dictionary<ProjectKey, IProjectSnapshot> _mappings;
    private readonly object _mappingsLock;

    [ImportingConstructor]
    public RazorProjectInfoEndpointPublisher(LSPRequestInvoker requestInvoker)
    {
        _requestInvoker = requestInvoker;
        _useCache = true;

        _mappings = new Dictionary<ProjectKey, IProjectSnapshot>();
        _mappingsLock = new object();
    }

    public void SendUpdate(IProjectSnapshot projectSnapshot, string configurationFilePath)
    {
        if (_useCache)
        {
            lock (_mappingsLock)
            {
                _mappings[projectSnapshot.Key] = projectSnapshot;
            }

            return;
        }
        else
        {
            SendImmediateUpdate(projectSnapshot, configurationFilePath);
        }
    }

    public void SendRemoval(IProjectSnapshot projectSnapshot)
    {
        if (_useCache)
        {
            lock (_mappingsLock)
            {
                _mappings.Remove(projectSnapshot.Key);
            }

            return;
        }
        else
        {
            SendImmediateUpdate(projectSnapshot.Key, encodedProjectInfo: null);
        }
    }

    private void SendImmediateUpdate(IProjectSnapshot projectSnapshot, string configurationFilePath)
    {
        using var stream = new MemoryStream();

        var projectInfo = projectSnapshot.ToRazorProjectInfo(configurationFilePath);
        projectInfo.SerializeTo(stream);
        var base64ProjectInfo = Convert.ToBase64String(stream.ToArray());

        SendImmediateUpdate(projectSnapshot.Key, base64ProjectInfo);
    }

    private void SendImmediateUpdate(ProjectKey projectKey, string? encodedProjectInfo)
    {
        var parameter = new ProjectInfoParams()
        {
            ProjectKeyId = projectKey.Id,
            ProjectInfo = encodedProjectInfo
        };

        _ = _requestInvoker.ReinvokeRequestOnServerAsync<ProjectInfoParams, object>(
                LanguageServerConstants.RazorProjectInfoEndpoint,
                RazorLSPConstants.RazorLanguageServerName,
                parameter,
                CancellationToken.None);
    }

    public void StopCachingRequests()
    {
        _useCache = false;
    }

    public void SerializeToEndpointUncached(ProjectKey projectKey, string configurationFilePath)
    {
        if (_mappings.TryGetValue(projectKey, out var projectSnapshot))
        {
            SendImmediateUpdate(projectSnapshot, configurationFilePath);
        }
    }

    public void ClearCache()
    {
        lock (_mappingsLock)
        {
            _mappings.Clear();
        }
    }
}
