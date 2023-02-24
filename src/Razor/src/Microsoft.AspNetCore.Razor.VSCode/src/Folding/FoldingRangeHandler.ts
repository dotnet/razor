/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { FoldingRange, FoldingRangeKind, RequestType } from 'vscode-languageclient';
import { RazorDocumentManager } from '../Document/RazorDocumentManager';
import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
import { RazorLogger } from '../RazorLogger';
import { SerializableFoldingRangeParams } from './SerializableFoldingRangeParams';
import { SerializableFoldingRangeResponse } from './SerializableFoldingRangeResponse';

export class FoldingRangeHandler {
    private static readonly provideFoldingRange = 'razor/foldingRange';
    private foldingRangeRequestType: RequestType<SerializableFoldingRangeParams, SerializableFoldingRangeResponse, any> = new RequestType(FoldingRangeHandler.provideFoldingRange);
    private emptyFoldingRangeReponse: SerializableFoldingRangeResponse = new SerializableFoldingRangeResponse(new Array<FoldingRange>(), new Array<FoldingRange>());

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

            const virtualHtmlUri = razorDocument.htmlDocument.uri;
            const virtualCSharpUri = razorDocument.csharpDocument.uri;

            const htmlFoldingRanges = await vscode.commands.executeCommand<vscode.FoldingRange[]>('vscode.executeFoldingRangeProvider', virtualHtmlUri);
            const csharpFoldingRanges = await vscode.commands.executeCommand<vscode.FoldingRange[]>('vscode.executeFoldingRangeProvider', virtualCSharpUri);

            const convertedHtmlFoldingRanges = htmlFoldingRanges === undefined ? new Array<FoldingRange>() : this.convertFoldingRanges(htmlFoldingRanges);
            const convertedCSharpFoldingRanges = csharpFoldingRanges === undefined ? new Array<FoldingRange>() : this.convertFoldingRanges(csharpFoldingRanges);

            const response = new SerializableFoldingRangeResponse(convertedHtmlFoldingRanges, convertedCSharpFoldingRanges);
            return response;
        } catch (error) {
            this.logger.logWarning(`${FoldingRangeHandler.provideFoldingRange} failed with ${error}`);
        }

        return this.emptyFoldingRangeReponse;
    }

    private convertFoldingRanges(foldingRanges: vscode.FoldingRange[]) {
        const convertedFoldingRanges = new Array<FoldingRange>();
        foldingRanges.forEach(foldingRange => {
            const convertedFoldingRange: FoldingRange = {
                startLine: foldingRange.start,
                startCharacter: 0,
                endLine: foldingRange.end,
                endCharacter: 0,
                kind: foldingRange.kind === undefined ? undefined : this.convertFoldingRangeKind(foldingRange.kind),
            };

            convertedFoldingRanges.push(convertedFoldingRange);
        });

        return convertedFoldingRanges;
    }

    private convertFoldingRangeKind(kind: vscode.FoldingRangeKind) {
        if (kind === vscode.FoldingRangeKind.Comment) {
            return FoldingRangeKind.Comment;
        } else if (kind === vscode.FoldingRangeKind.Imports) {
            return FoldingRangeKind.Imports;
        } else if (kind === vscode.FoldingRangeKind.Region) {
            return FoldingRangeKind.Region;
        } else {
            this.logger.logWarning(`Unexpected FoldingRangeKind ${kind}`);
            return undefined;
        }
    }
}
