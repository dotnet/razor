/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
import { RazorLogger } from '../RazorLogger';
import { CodeActionComputationResponse } from '../RPC/CodeActionComputationResponse';

export class RazorCodeActionRunner {

    constructor(
        private readonly serverClient: RazorLanguageServerClient,
        private readonly logger: RazorLogger,
    ) {}

    public register() {
        vscode.commands.registerCommand('razor/runCodeAction', (request: object) => this.runCodeAction(request), this);
    }

    private async runCodeAction(request: any): Promise<boolean | string | {}> {
        const response: CodeActionComputationResponse = await this.serverClient.sendRequest('razor/resolveCodeAction', {Action: request.Action, Data: request.Data});
        const workspaceEdit = new vscode.WorkspaceEdit();
        try {
            if (Array.isArray(response.edit.documentChanges)) {
                for (const documentChange of response.edit.documentChanges) {
                    if (documentChange.kind === 'create') {
                        workspaceEdit.createFile(vscode.Uri.parse(documentChange.uri), documentChange.options);
                    } else if (documentChange.kind === 'rename') {
                        workspaceEdit.renameFile(vscode.Uri.parse(documentChange.oldUri), vscode.Uri.parse(documentChange.newUri), documentChange.options);
                    } else if (documentChange.kind === 'delete') {
                        workspaceEdit.deleteFile(vscode.Uri.parse(documentChange.uri), documentChange.options);
                    }
                }
            }

            if (response.edit.changes !== undefined) {
                for (const uri in response.edit.changes) {
                    if (!response.edit.changes.hasOwnProperty(uri)) {
                        continue;
                    }
                    const changes = response.edit.changes[uri];
                    for (const change of changes) {
                        const range = new vscode.Range(
                            change.range.start.line,
                            change.range.start.character,
                            change.range.end.line,
                            change.range.end.character);
                        workspaceEdit.replace(vscode.Uri.parse(uri), range, change.newText);
                    }
                }
            }
        } catch (e) {
            this.logger.logAlways(`caught error running code action: ${e}`);
            return Promise.resolve(false);
        }

        return vscode.workspace.applyEdit(workspaceEdit);
    }
}
