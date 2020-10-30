/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import {
    ClientCapabilities,
    DocumentSelector,
    InitializeParams,
    RequestType,
    ServerCapabilities,
    StaticFeature,
} from 'vscode-languageclient';
import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
import { SerializableSemanticTokensParams } from '../RPC/SerializableSemanticTokensParams';
import { SemanticTokensResponse } from './SemanticTokensResponse';

export class SemanticTokensFeature implements StaticFeature {
    private static readonly getSemanticTokensEndpoint = 'razor/provideSemanticTokens';
    public fillInitializeParams?: ((params: InitializeParams) => void) | undefined;
    private semanticTokensRequestType: RequestType<SerializableSemanticTokensParams, SemanticTokensResponse, any, any> = new RequestType(SemanticTokensFeature.getSemanticTokensEndpoint);
    private emptySemanticTokensResponse: SemanticTokensResponse = new SemanticTokensResponse(new Array<number>(), '');

    constructor(private readonly serverClient: RazorLanguageServerClient) {
    }

    public fillClientCapabilities(capabilities: ClientCapabilities): void {
        return;
    }

    public async initialize(capabilities: ServerCapabilities, documentSelector: DocumentSelector | undefined): Promise<void> {
        await this.serverClient.onRequestWithParams<SerializableSemanticTokensParams, SemanticTokensResponse, any, any>(
            this.semanticTokensRequestType,
            async (request, token) => this.getSemanticTokens(request, token));
    }

    private async getSemanticTokens(
        semanticTokensParams: SerializableSemanticTokensParams,
        cancellationToken: vscode.CancellationToken) {

        // This is currently a No-Op because we don't have a way to get the semantic tokens from CSharp.
        // Other functions accomplish this with `vscode.execute<Blank>Provider`, but that doesn't exiset for Semantic Tokens yet because it's still not an official part of the spec.
        return this.emptySemanticTokensResponse;
    }
}
