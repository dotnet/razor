/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { SerializableTextDocumentIdentifier } from './../RPC/SerializableTextDocumentIdentifier';

export interface SerializableFormattingParams {
    textDocument: SerializableTextDocumentIdentifier;
    options: vscode.FormattingOptions;
}
