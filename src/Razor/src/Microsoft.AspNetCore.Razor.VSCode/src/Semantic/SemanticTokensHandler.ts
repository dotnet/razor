/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { SemanticTokens } from 'vscode';
import { RequestType } from 'vscode-languageclient';
import { RazorDocumentManager } from '../RazorDocumentManager';
import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
import { RazorLogger } from '../RazorLogger';
import { convertRangeFromSerializable } from '../RPC/SerializableRange';
import { SerializableSemanticTokensParams } from '../RPC/SerializableSemanticTokensParams';

export class SemanticTokensHandler {
    private static readonly getSemanticTokensEndpoint = 'razor/provideSemanticTokens';
    private semanticTokensRequestType: RequestType<SerializableSemanticTokensParams, vscode.SemanticTokens, any, any> = new RequestType(SemanticTokensHandler.getSemanticTokensEndpoint);
    private emptySemanticTokensResponse: SemanticTokens = new vscode.SemanticTokens(new Uint32Array());

    constructor(
        private readonly documentManager: RazorDocumentManager,
        private readonly serverClient: RazorLanguageServerClient,
        private readonly logger: RazorLogger) {
    }

    public register() {
        // tslint:disable-next-line: no-floating-promises
        this.serverClient.onRequestWithParams<SerializableSemanticTokensParams, vscode.SemanticTokens, any, any>(
            this.semanticTokensRequestType,
            async (request, token) => this.getSemanticTokens(request, token));
    }

    private async getSemanticTokens(
        semanticTokensParams: SerializableSemanticTokensParams,
        cancellationToken: vscode.CancellationToken) {
        try {
            const razorDocumentUri = vscode.Uri.parse(semanticTokensParams.textDocument.uri);
            const razorDocument = await this.documentManager.getDocument(razorDocumentUri);
            if (razorDocument === undefined) {
                return this.emptySemanticTokensResponse;
            }

            const virtualCSharpUri = razorDocument.csharpDocument.uri;

            let range: vscode.Range | undefined;
            if (semanticTokensParams.range !== undefined) {
                range = convertRangeFromSerializable(semanticTokensParams.range);
            }
            const commands = await vscode.commands.getCommands(true);
            for (const command of commands) {
                if (command.lastIndexOf('semantic') >= 0 || command.lastIndexOf('Semantic') >= 0) {
                    console.log(command);
                }
            }
            const semanticTokens = await vscode.commands.executeCommand<vscode.SemanticTokens>(
                'vscode.executeDocumentColorProvider',
                virtualCSharpUri,
                range) as vscode.SemanticTokens;

            if (semanticTokens === undefined) {
                return this.emptySemanticTokensResponse;
            }

            return semanticTokens;
        } catch (error) {
            this.logger.logWarning(`${SemanticTokensHandler.getSemanticTokensEndpoint} failed with ${error}`);
        }

        return this.emptySemanticTokensResponse;
    }
}
