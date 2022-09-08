/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { RequestType } from 'vscode-languageclient';
import { RazorDocumentManager } from '../RazorDocumentManager';
import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
import { RazorLogger } from '../RazorLogger';
import { SerializableTextDocumentIdentifier } from '../RPC/SerializableTextDocumentIdentifier';

export class DocumentColorHandler {
    private static readonly provideHtmlDocumentColorEndpoint = 'razor/provideHtmlDocumentColor';
    private documentColorRequestType: RequestType<SerializableTextDocumentIdentifier, vscode.ColorInformation[], any> = new RequestType(DocumentColorHandler.provideHtmlDocumentColorEndpoint);
    private emptyColorInformationResponse: vscode.ColorInformation[] = [];

    constructor(
        private readonly documentManager: RazorDocumentManager,
        private readonly serverClient: RazorLanguageServerClient,
        private readonly logger: RazorLogger) {
    }

    public register() {
        // tslint:disable-next-line: no-floating-promises
        this.serverClient.onRequestWithParams<SerializableTextDocumentIdentifier, vscode.ColorInformation[], any>(
            this.documentColorRequestType,
            async (request: SerializableTextDocumentIdentifier, token: vscode.CancellationToken) => this.provideHtmlDocumentColors(request, token));
    }

    private async provideHtmlDocumentColors(
        documentColorParams: SerializableTextDocumentIdentifier,
        cancellationToken: vscode.CancellationToken) {
        try {
            const razorDocumentUri = vscode.Uri.parse(`vscode:${documentColorParams.uri}`, true);
            const razorDocument = await this.documentManager.getDocument(razorDocumentUri);
            if (razorDocument === undefined) {
                this.logger.logWarning(`Could not find Razor document ${razorDocumentUri}; returning empty color information.`);
                return this.emptyColorInformationResponse;
            }

            const virtualHtmlUri = razorDocument.htmlDocument.uri;

            const commands = await vscode.commands.executeCommand<vscode.Command[]>(
                'vscode.executeDocumentColorProvider',
                virtualHtmlUri) as vscode.Command[];

            if (commands.length === 0) {
                return this.emptyColorInformationResponse;
            }

            return commands.map(c => this.commandAsCodeAction(c));
        } catch (error) {
            this.logger.logWarning(`${DocumentColorHandler.provideHtmlDocumentColorEndpoint} failed with ${error}`);
        }

        return this.emptyColorInformationResponse;
    }

    // TO-DO: Fill in the below method:
    // https://github.com/dotnet/razor-tooling/issues/6806
    private commandAsCodeAction(command: vscode.Command): vscode.ColorInformation {
        return { } as vscode.ColorInformation;
    }
}
