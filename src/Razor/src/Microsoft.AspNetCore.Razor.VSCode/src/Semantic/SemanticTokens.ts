/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

export class SemanticTokens {
    public readonly resultId?: string;

    public readonly data: Uint32Array;

    constructor(data: Uint32Array, resultId?: string) {
        this.resultId = resultId;
        this.data = data;
    }
}
