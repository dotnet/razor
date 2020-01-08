/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { IRazorCodeActionTranslator } from './IRazorCodeActionTranslator';

export class RazorCodeActionTranslatorManager {

    constructor(private readonly codeActionTranslators: IRazorCodeActionTranslator[]) { }

    public canHandle(
        codeAction: vscode.Command,
        codeContext: vscode.CodeActionContext,
        document: vscode.TextDocument,
    ): boolean {
        for (const actionTranslator of this.codeActionTranslators) {
            if (actionTranslator.canHandleCodeAction(codeAction, codeContext, document)) {
                return true;
            }
        }

        return false;
    }

    public applyEdit(
        uri: vscode.Uri,
        edit: vscode.TextEdit,
    ): [vscode.Uri?, vscode.TextEdit?] {
        for (const actionTranslator of this.codeActionTranslators) {
            if (actionTranslator.canHandleEdit(uri, edit)) {
                return actionTranslator.applyEdit(uri, edit);
            }
        }
        throw new Error('ApplyEdit should always be handled by one of the ActionTranslators because it should either automatically remap or they should manually fiddle with it.');
    }
}
