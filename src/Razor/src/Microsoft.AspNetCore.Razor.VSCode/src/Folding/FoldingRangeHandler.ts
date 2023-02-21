/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { RequestType } from 'vscode-languageclient';
import { RazorDocumentManager } from '../Document/RazorDocumentManager';
import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
import { RazorLogger } from '../RazorLogger';
import { SerializableFoldingRangeParams } from './SerializableFoldingRangeParams';
import { SerializableFoldingRangeResponse } from './SerializableFoldingRangeResponse';

export class FoldingRangeHandler {
    private static readonly provideFoldingRange = 'razor/foldingRange';
    private foldingRangeRequestType: RequestType<SerializableFoldingRangeParams, SerializableFoldingRangeResponse, any> = new RequestType(FoldingRangeHandler.provideFoldingRange);
    private emptyFoldingRangeReponse: SerializableFoldingRangeResponse = new SerializableFoldingRangeResponse(new Array<vscode.FoldingRange>(), new Array<vscode.FoldingRange>());

    constructor(
        private readonly serverClient: RazorLanguageServerClient,
        private readonly documentManager: RazorDocumentManager,
        private readonly logger: RazorLogger) { }

    public register() {
        // tslint:disable-next-line: no-floating-promises
        this.serverClient.onRequestWithParams<SerializableFoldingRangeParams, SerializableFoldingRangeResponse, any>(
            this.foldingRangeRequestType,
            async (request, token) => this.provideFoldingRanges(request, token));
    }

    private async provideFoldingRanges(
        foldingRangeParams: SerializableFoldingRangeParams,
        cancellationToken: vscode.CancellationToken) {
        try {
            const razorDocumentUri = vscode.Uri.parse(foldingRangeParams.textDocument.uri, true);
            const razorDocument = await this.documentManager.getDocument(razorDocumentUri);
            if (razorDocument === undefined) {
                return this.emptyFoldingRangeReponse;
            }

            const virtualCSharpUri = razorDocument.csharpDocument.uri;
            const virtualHtmlUri = razorDocument.htmlDocument.uri;

            const csharpFoldingRanges = await vscode.commands.executeCommand<vscode.FoldingRange[]>('vscode.executeFoldingRangeProvider', virtualCSharpUri);
            const htmlFoldingRanges = await vscode.commands.executeCommand<vscode.FoldingRange[]>('vscode.executeFoldingRangeProvider', virtualHtmlUri);

            const response = new SerializableFoldingRangeResponse(csharpFoldingRanges, htmlFoldingRanges);
            return response;
        } catch (error) {
            this.logger.logWarning(`${FoldingRangeHandler.provideFoldingRange} failed with ${error}`);
        }

        return this.emptyFoldingRangeReponse;
    }
}
