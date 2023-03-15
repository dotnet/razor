﻿/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { RequestType } from 'vscode-languageclient';
import { RazorDocumentManager } from '../Document/RazorDocumentManager';
import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
import { RazorLogger } from '../RazorLogger';
import { LanguageKind } from '../RPC/LanguageKind';
import { convertRangeFromSerializable } from '../RPC/SerializableRange';
import { convertTextEditToSerializable } from '../RPC/SerializableTextEdit';
import { RazorDocumentRangeFormattingRequest } from './RazorDocumentRangeFormattingRequest';
import { RazorDocumentRangeFormattingResponse } from './RazorDocumentRangeFormattingResponse';

export class RazorFormattingFeature {

    private rangeFormattingRequestType: RequestType<RazorDocumentRangeFormattingRequest, RazorDocumentRangeFormattingResponse, any> = new RequestType('razor/rangeFormatting');
    private emptyRangeFormattingResponse: RazorDocumentRangeFormattingResponse = new RazorDocumentRangeFormattingResponse([]);

    constructor(
        private readonly serverClient: RazorLanguageServerClient,
        private readonly documentManager: RazorDocumentManager,
        private readonly logger: RazorLogger) {
    }

    public register() {
        return this.serverClient.onRequestWithParams<RazorDocumentRangeFormattingRequest, RazorDocumentRangeFormattingResponse, any>(
            this.rangeFormattingRequestType,
            async (request, token) => this.handleRangeFormatting(request, token));
    }

    private async handleRangeFormatting(request: RazorDocumentRangeFormattingRequest, token: vscode.CancellationToken) {
        if (request.kind === LanguageKind.Razor) {
            // We shouldn't attempt to format the actual Razor document here.
            // Doing so could potentially lead to an infinite loop.
            return this.emptyRangeFormattingResponse;
        }

        try {
            const uri = vscode.Uri.file(request.hostDocumentFilePath);
            const razorDocument = await this.documentManager.getDocument(uri);
            if (!razorDocument) {
                return this.emptyRangeFormattingResponse;
            }

            let documentUri = uri;
            if (request.kind === LanguageKind.CSharp) {
                documentUri = razorDocument.csharpDocument.uri;
            } else {
                documentUri = razorDocument.htmlDocument.uri;
            }

            // Get the edits
            const textEdits = await vscode.commands.executeCommand<vscode.TextEdit[]>(
                'vscode.executeFormatRangeProvider',
                documentUri,
                convertRangeFromSerializable(request.projectedRange),
                request.options);

            if (textEdits) {
                const edits = textEdits.map(item => convertTextEditToSerializable(item));
                return new RazorDocumentRangeFormattingResponse(edits);
            }
        } catch (error) {
            this.logger.logWarning(`razor/rangeFormatting failed with ${error}`);
        }

        return this.emptyRangeFormattingResponse;
    }
}
