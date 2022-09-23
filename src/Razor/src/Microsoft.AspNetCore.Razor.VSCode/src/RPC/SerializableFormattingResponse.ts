/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import { SerializableTextEdit } from './SerializableTextEdit';

export class SerializableFormattingResponse {
    public readonly edits: SerializableTextEdit[];

    constructor(edits?: SerializableTextEdit[]) {
        if (edits == null) {
            this.edits = new Array<SerializableTextEdit>();
        } else {
            this.edits = edits;
        }
    }
}
