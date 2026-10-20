// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

module.exports = {
  transform: {
    '^.+\\.tsx?$': ['ts-jest', { tsconfig: './tsconfig.json', diagnostics: true }]
  },
  testPathIgnorePatterns: [ 'dist' ],
  // These tests tokenize files with Oniguruma; they do not use a browser DOM.
  testEnvironment: 'node',
  // Keep the existing snapshots comparable when upgrading Jest.
  snapshotFormat: { escapeString: true, printBasicPrototype: true }
};
