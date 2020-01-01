/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { IRazorCodeActionTranslator } from './IRazorCodeActionTranslator';

export class RazorFullyQualifiedCodeActionTranslator implements IRazorCodeActionTranslator {

    private static expectedCode = 'CS0246';

    public applyEdit(
        uri: vscode.Uri,
        edit: vscode.TextEdit): [vscode.Uri | undefined, vscode.TextEdit | undefined] {
        // The edit for this should just translate without additional help.
        throw new Error('Method not implemented.');
    }

    public canHandleEdit(uri: vscode.Uri, edit: vscode.TextEdit): boolean {
        // The edit for this should just translate without additional help.
        return false;
    }

    public canHandleCodeAction(
        codeAction: vscode.Command,
        codeContext: vscode.CodeActionContext,
        document: vscode.TextDocument): boolean {
        const isMissingDiag = (value: vscode.Diagnostic) => {
            return value.severity === vscode.DiagnosticSeverity.Error
            && value.code === RazorFullyQualifiedCodeActionTranslator.expectedCode;
        };

        const diagnostic = codeContext.diagnostics.find(isMissingDiag);
        if (diagnostic) {
            const codeRange = diagnostic.range;
            const codeValue = document.getText(codeRange);
            if (codeAction.arguments) {
                if (!codeAction.title.includes(' ')
                    && codeAction.title.endsWith(codeValue)) {
                    return true;
                }
            }
        }

        return false;
    }
}
