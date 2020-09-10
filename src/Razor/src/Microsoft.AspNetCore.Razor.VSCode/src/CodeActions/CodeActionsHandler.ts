/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { RequestType } from 'vscode-languageclient';
import { RazorDocumentManager } from '../RazorDocumentManager';
import { RazorDocumentSynchronizer } from '../RazorDocumentSynchronizer';
import { RazorLanguageFeatureBase } from '../RazorLanguageFeatureBase';
import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
import { RazorLanguageServiceClient } from '../RazorLanguageServiceClient';
import { RazorLogger } from '../RazorLogger';
import { RazorCodeAction } from '../RPC/RazorCodeAction';
import { SerializableCodeActionParams } from '../RPC/SerializableCodeActionParams';
// import { RazorDocumentManager } from './RazorDocumentManager';
// import { RazorLogger } from './RazorLogger';
// import { LanguageKind } from './RPC/LanguageKind';
// import { RazorDocumentRangeFormattingRequest } from './RPC/RazorDocumentRangeFormattingRequest';
// import { RazorDocumentRangeFormattingResponse } from './RPC/RazorDocumentRangeFormattingResponse';
// import { convertRangeFromSerializable } from './RPC/SerializableRange';
// import { convertTextEditToSerializable } from '../RPC/SerializableTextEdit';

export class CodeActionsHandler
    extends RazorLanguageFeatureBase {

    private static readonly getCodeActionsEndpoint = 'razor/getCodeActions';
    private codeActionRequestType: RequestType<SerializableCodeActionParams, RazorCodeAction[], any, any> = new RequestType(CodeActionsHandler.getCodeActionsEndpoint);
    private emptyCodeActionResponse: RazorCodeAction[] = [];

    constructor(
        documentSynchronizer: RazorDocumentSynchronizer,
        documentManager: RazorDocumentManager,
        serviceClient: RazorLanguageServiceClient,
        logger: RazorLogger) {
            super(documentSynchronizer, documentManager, serviceClient, logger);
    }

    public register(serverClient: RazorLanguageServerClient) {
        // tslint:disable-next-line: no-floating-promises
        serverClient.onRequestWithParams<SerializableCodeActionParams, RazorCodeAction[], any, any>(
            this.codeActionRequestType,
            async (request, token) => this.getCodeActions(request, token));
    }

    // tslint:disable-next-line: no-empty
    // public register() {
    // }

    private async getCodeActions(
        codeActionParams: SerializableCodeActionParams,
        // document: vscode.TextDocument,
        // range: vscode.Range | vscode.Selection,
        // context: vscode.CodeActionContext,
        token: vscode.CancellationToken) {

        // return this.emptyCodeActionResponse;

        const textDocument = vscode.workspace.textDocuments.find(d => d.uri.toString() === codeActionParams.textDocument.uri);

        if (textDocument === undefined) {
            return this.emptyCodeActionResponse;
        }

        try {
            const startPosition = new vscode.Position(codeActionParams.range.start.line, codeActionParams.range.start.character);
            const startProjection = await this.getProjection(textDocument, startPosition, token);
            if (!startProjection) {
                return this.emptyCodeActionResponse;
            }

            const endPosition = new vscode.Position(codeActionParams.range.end.line, codeActionParams.range.end.character);
            const endProjection = await this.getProjection(textDocument, endPosition, token);
            if (!endProjection) {
                return this.emptyCodeActionResponse;
            }

            // This is just a sanity check, they should always be the same.
            if (startProjection.uri !== endProjection.uri) {
                return this.emptyCodeActionResponse;
            }

            const projectedRange = new vscode.Range(startProjection.position, endProjection.position);

            const commands = await vscode.commands.executeCommand<vscode.Command[]>(
                'vscode.executeCodeActionProvider',
                startProjection.uri,
                projectedRange) as vscode.Command[];

            if (commands.length > 0) {
                return this.emptyCodeActionResponse;
            }

            // const razorCodeActions = commands.map(c => return { title: c.title });
        } catch (error) {
            this.logger.logWarning(`${CodeActionsHandler.getCodeActionsEndpoint} failed with ${error}`);
        }

        return this.emptyCodeActionResponse;
    }

    // private async handleRangeFormatting(request: RazorDocumentRangeFormattingRequest, token: CancellationToken) {
    //     if (request.kind === LanguageKind.Razor) {
    //         // We shouldn't attempt to format the actual Razor document here.
    //         // Doing so could potentially lead to an infinite loop.
    //         return this.emptyCodeActionResponse;
    //     }

    //     try {
    //         const uri = vscode.Uri.file(request.hostDocumentFilePath);
    //         const razorDocument = await this.documentManager.getDocument(uri);
    //         if (!razorDocument) {
    //             return this.emptyCodeActionResponse;
    //         }

    //         let documentUri = uri;
    //         if (request.kind === LanguageKind.CSharp) {
    //             documentUri = razorDocument.csharpDocument.uri;
    //         } else {
    //             documentUri = razorDocument.htmlDocument.uri;
    //         }

    //         // Get the edits
    //         const textEdits = await vscode.commands.executeCommand<vscode.TextEdit[]>(
    //             'vscode.executeFormatRangeProvider',
    //             documentUri,
    //             convertRangeFromSerializable(request.projectedRange),
    //             request.options);

    //         if (textEdits) {
    //             const edits = textEdits.map(item => convertTextEditToSerializable(item));
    //             return new RazorDocumentRangeFormattingResponse(edits);
    //         }
    //     } catch (error) {
    //         this.logger.logWarning(`razor/rangeFormatting failed with ${error}`);
    //     }

    //     return this.emptyCodeActionResponse;
    // }
}
