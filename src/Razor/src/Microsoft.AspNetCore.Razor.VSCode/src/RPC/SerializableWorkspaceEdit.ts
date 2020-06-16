/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { SerializableCreateDocument } from './SerializableCreateDocument';
import { SerializableDeleteDocument } from './SerializableDeleteDocument';
import { convertRangeFromSerializable } from './SerializableRange';
import { SerializableRenameDocument } from './SerializableRenameDocument';
import { SerializableTextEdit } from './SerializableTextEdit';

type SerializableDocumentChange = SerializableCreateDocument | SerializableRenameDocument | SerializableDeleteDocument;

export interface SerializableWorkspaceEdit {
    changes?: {[key: string]: Array<SerializableTextEdit>};
    documentChanges?: Array<SerializableDocumentChange>;
}

export function convertWorkspaceEditFromSerializable(data: SerializableWorkspaceEdit): vscode.WorkspaceEdit {
    const workspaceEdit = new vscode.WorkspaceEdit();
    if (Array.isArray(data.documentChanges)) {
        for (const documentChange of data.documentChanges) {
            if (documentChange.kind === 'create') {
                workspaceEdit.createFile(vscode.Uri.parse(documentChange.uri), documentChange.options);
            } else if (documentChange.kind === 'rename') {
                workspaceEdit.renameFile(vscode.Uri.parse(documentChange.oldUri), vscode.Uri.parse(documentChange.newUri), documentChange.options);
            } else if (documentChange.kind === 'delete') {
                workspaceEdit.deleteFile(vscode.Uri.parse(documentChange.uri), documentChange.options);
            }
        }
    }
    if (data.changes !== undefined) {
        for (const uri in data.changes) {
            if (!data.changes.hasOwnProperty(uri)) {
                continue;
            }
            const changes = data.changes[uri];
            for (const change of changes) {
                const range = convertRangeFromSerializable(change.range);
                workspaceEdit.replace(vscode.Uri.parse(uri), range, change.newText);
            }
        }
    }
    return workspaceEdit;
}
