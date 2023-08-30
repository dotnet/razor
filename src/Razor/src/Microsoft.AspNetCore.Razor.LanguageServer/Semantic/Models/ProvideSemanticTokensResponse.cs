// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

/// <summary>
/// Transports C# semantic token responses from the Razor LS client to the Razor LS.
/// </summary>
internal class ProvideSemanticTokensResponse
{
    public ProvideSemanticTokensResponse(int[][]? tokens, long? hostDocumentSyncVersion)
    {
        Tokens = tokens;
        HostDocumentSyncVersion = hostDocumentSyncVersion;
    }

    public int[][]? Tokens { get; }

    public long? HostDocumentSyncVersion { get; }

    public override bool Equals(object? obj)
    {
        if (obj is not ProvideSemanticTokensResponse other ||
            HostDocumentSyncVersion != other.HostDocumentSyncVersion)
        {
            return false;
        }

        if (Tokens is not null && other.Tokens is not null)
        {
            if (Tokens?.Length != other.Tokens?.Length)
            {
                return false;
            }

            for (var i = 0; i < Tokens!.Length; i++)
            {
                if (Tokens[i].Length != other.Tokens![i].Length)
                {
                    return false;
                }

                for (var j = 0; j < Tokens[i].Length; j++)
                {
                    if (Tokens[i][j] != other.Tokens[i][j])
                    {
                        return false;
                    }
                }
            }
        }

        return Tokens is null && other.Tokens is null;
    }

    public override int GetHashCode()
    {
        throw new System.NotImplementedException();
    }
}
