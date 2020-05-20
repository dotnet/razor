/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { LanguageKind } from '../RPC/LanguageKind';
import { convertRangeToSerializable, SerializableRange } from '../RPC/SerializableRange';

export class SemanticTokensRangeRequest {
    public readonly razorDocumentUri: string;
    public readonly range: SerializableRange;

    constructor(
        public readonly kind: LanguageKind,
        razorDocumentUri: vscode.Uri,
        range: vscode.Range,
    ) {
        this.razorDocumentUri = razorDocumentUri.toString();
        this.range = convertRangeToSerializable(range);
    }
}
