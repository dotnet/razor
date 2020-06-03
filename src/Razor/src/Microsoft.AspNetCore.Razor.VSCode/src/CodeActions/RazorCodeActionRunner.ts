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
        vscode.commands.registerCommand('razor/runCodeAction', (action: string, ...args: any[]) => {
            this
                .runCodeAction(action, args)
                .then()
                .catch(() => this.logger.logAlways('caught exception running code action'));
        }, this);
        this.logger.logAlways('registered code action runner');
    }

    private async runCodeAction(action: string, args: string[]): Promise<boolean | string | {}> {
        const response: CodeActionComputationResponse = await this.serverClient.sendRequest('razor/codeActionComputation', {Action: action, Arguments: args});
        this.logger.logMessage(`Received computed workspace edit ${JSON.stringify(response)}`);

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
            this.logger.logAlways(`caught in run code action: ${e}`);
            return Promise.resolve(false);
        }

        const result = await vscode.workspace.applyEdit(workspaceEdit);
        this.logger.logAlways(` status ${result}`);

        return true;

        //     // Unfortunately, the textEditor.Close() API has been deprecated
        //     // and replaced with a command that can only close the active editor.
        //     // If files were renamed that weren't the active editor, their tabs will
        //     // be left open and marked as "deleted" by VS Code
        //     let next = applyEditPromise;
        //     if (renamedFiles.some(r => r.fsPath == vscode.window.activeTextEditor.document.uri.fsPath))
        //     {
        //         next = applyEditPromise.then(_ =>
        //             {
        //                 return vscode.commands.executeCommand("workbench.action.closeActiveEditor");
        //             });
        //     }

        //     return fileToOpen != null
        //         ? next.then(_ =>
        //             {
        //                 return vscode.commands.executeCommand("vscode.open", fileToOpen);
        //             })
        //         : next;
        //     }
        // }
    }
}
