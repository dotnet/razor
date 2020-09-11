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
import { convertRangeFromSerializable } from '../RPC/SerializableRange';

export class CodeActionsHandler
    extends RazorLanguageFeatureBase {

    private static readonly getCodeActionsEndpoint = 'razor/getCodeActions';
    private codeActionRequestType: RequestType<SerializableCodeActionParams, RazorCodeAction[], any, any> = new RequestType(CodeActionsHandler.getCodeActionsEndpoint);
    private emptyCodeActionResponse: RazorCodeAction[] = [];

    constructor(
        documentSynchronizer: RazorDocumentSynchronizer,
        documentManager: RazorDocumentManager,
        serviceClient: RazorLanguageServiceClient,
        private readonly serverClient: RazorLanguageServerClient,
        logger: RazorLogger) {
            super(documentSynchronizer, documentManager, serviceClient, logger);
    }

    public register() {
        // tslint:disable-next-line: no-floating-promises
        this.serverClient.onRequestWithParams<SerializableCodeActionParams, RazorCodeAction[], any, any>(
            this.codeActionRequestType,
            async (request, token) => this.getCodeActions(request, token));
    }

    private async getCodeActions(
        codeActionParams: SerializableCodeActionParams,
        token: vscode.CancellationToken) {
        try {
            const razorDocumentUri = vscode.Uri.parse(codeActionParams.textDocument.uri);
            const razorDocument = await this.documentManager.getDocument(razorDocumentUri);
            const virtualCsharpUri = razorDocument.csharpDocument.uri;

            const range = convertRangeFromSerializable(codeActionParams.range);

            const commands = await vscode.commands.executeCommand<vscode.Command[]>(
                'vscode.executeCodeActionProvider',
                virtualCsharpUri,
                range) as vscode.Command[];

            if (commands.length === 0) {
                return this.emptyCodeActionResponse;
            }

            return commands.map(c => this.commandAsCodeAction(c));
        } catch (error) {
            this.logger.logWarning(`${CodeActionsHandler.getCodeActionsEndpoint} failed with ${error}`);
        }

        return this.emptyCodeActionResponse;
    }

    private commandAsCodeAction(command: vscode.Command): RazorCodeAction {
        return { title: command.title } as RazorCodeAction;
    }
}
