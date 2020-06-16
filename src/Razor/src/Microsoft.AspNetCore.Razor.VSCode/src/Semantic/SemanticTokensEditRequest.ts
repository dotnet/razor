/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { TextDocumentIdentifier } from 'vscode-languageclient';
import { LanguageKind } from '../RPC/LanguageKind';

export class SemanticTokensEditRequest {
    public readonly textDocument: TextDocumentIdentifier;

    constructor(
        public readonly kind: LanguageKind,
        razorDocumentUri: vscode.Uri,
        public readonly previousResultId: string,
    ) {
        this.textDocument = TextDocumentIdentifier.create(razorDocumentUri.toString());
    }
}
