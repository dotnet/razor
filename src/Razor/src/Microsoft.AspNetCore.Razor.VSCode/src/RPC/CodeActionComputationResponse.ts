/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

export interface OmniSharpTextEdit {
    range: {
        start: {
            line: number,
            character: number,
        },
        end: {
            line: number,
            character: number,
        },
    };
    newText: string;
}

export interface OmniSharpCreateDocument {
    kind: 'create';
    uri: string;
    options: {
        overwrite: boolean;
        ignoreIfExists: boolean;
    };
}

export interface OmniSharpRenameDocument {
    kind: 'rename';
    oldUri: string;
    newUri: string;
    options: {
        overwrite: boolean;
        ignoreIfExists: boolean;
    };
}

export interface OmniSharpDeleteDocument {
    kind: 'delete';
    uri: string;
    options: {
        recursive: boolean;
        ignoreIfNotExists: boolean;
    };
}

export type OmniSharpDocumentChange = OmniSharpCreateDocument | OmniSharpRenameDocument | OmniSharpDeleteDocument;

export interface OmniSharpWorkspaceEdit {
    changes?: {[key: string]: Array<OmniSharpTextEdit>};
    documentChanges?: Array<OmniSharpDocumentChange>;
}

export interface CodeActionComputationResponse {
    edit: OmniSharpWorkspaceEdit;
}
