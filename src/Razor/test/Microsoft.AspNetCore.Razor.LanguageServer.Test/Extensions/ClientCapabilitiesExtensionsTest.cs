// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using Xunit;
using OmniSharpClientCapabilities = OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities.ClientCapabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    public class ClientCapabilitiesExtensionsTest
    {
        [Fact]
        public void ToVSClientCapabilities_WorksAsExpected()
        {
            // Arrange
            var clientCapabilitiesJson = """
                {
                  "_vs_supportsVisualStudioExtensions": true,
                  "_vs_supportedSnippetVersion": 1,
                  "_vs_supportsIconExtensions": true,
                  "_vs_supportsDiagnosticRequests": true,
                  "workspace": {
                    "applyEdit": true,
                    "workspaceEdit": {
                      "documentChanges": true,
                      "resourceOperations": []
                    },
                    "didChangeConfiguration": {},
                    "didChangeWatchedFiles": {},
                    "symbol": {},
                    "executeCommand": {
                      "_vs_supportedCommands": [
                        "_ms_setClipboard",
                        "_ms_openUrl"
                      ]
                    },
                    "semanticTokens": {
                      "RefreshSupport": true
                    }
                  },
                  "textDocument": {
                    "_vs_onAutoInsert": {},
                    "synchronization": {
                      "didSave": true
                    },
                    "completion": {
                      "_vs_completionList": {
                        "_vs_data": true,
                        "_vs_commitCharacters": true
                      },
                      "completionItem": {},
                      "completionItemKind": {
                        "valueSet": [
                          0,
                          1,
                          2,
                          3,
                          4,
                          5,
                          6,
                          7,
                          8,
                          9,
                          10,
                          11,
                          12,
                          13,
                          14,
                          15,
                          16,
                          17,
                          18,
                          19,
                          20,
                          21,
                          22,
                          23,
                          24,
                          25,
                          118115,
                          118116,
                          118117,
                          118118,
                          118119,
                          118120,
                          118121,
                          118122,
                          118123,
                          118124,
                          118125,
                          118126
                        ]
                      },
                      "completionList": {
                        "itemDefaults": [
                          "commitCharacters",
                          "editRange",
                          "insertTextFormat"
                        ]
                      }
                    },
                    "hover": {
                      "contentFormat": [
                        "plaintext"
                      ]
                    },
                    "signatureHelp": {
                      "signatureInformation": {
                        "documentationFormat": [
                          "plaintext"
                        ],
                        "parameterInformation": {
                          "labelOffsetSupport": true
                        }
                      },
                      "contextSupport": true
                    },
                    "definition": {},
                    "typeDefinition": {},
                    "implementation": {},
                    "references": {},
                    "documentHighlight": {},
                    "documentSymbol": {},
                    "codeAction": {
                      "codeActionLiteralSupport": {
                        "_vs_codeActionGroup": {
                          "_vs_valueSet": [
                            "quickfix",
                            "refactor",
                            "refactor.extract",
                            "refactor.inline",
                            "refactor.rewrite",
                            "source",
                            "source.organizeImports"
                          ]
                        },
                        "codeActionKind": {
                          "valueSet": [
                            "quickfix",
                            "refactor",
                            "refactor.extract",
                            "refactor.inline",
                            "refactor.rewrite",
                            "source",
                            "source.organizeImports"
                          ]
                        }
                      },
                      "resolveSupport": {
                        "properties": [
                          "additionalTextEdits",
                          "command",
                          "commitCharacters",
                          "description",
                          "detail",
                          "documentation",
                          "insertText",
                          "insertTextFormat",
                          "label"
                        ]
                      },
                      "dataSupport": true
                    },
                    "codeLens": {},
                    "documentLink": {},
                    "formatting": {},
                    "rangeFormatting": {},
                    "onTypeFormatting": {},
                    "rename": {},
                    "publishDiagnostics": {
                      "tagSupport": {
                        "valueSet": [
                          1,
                          2,
                          -1,
                          -2
                        ]
                      }
                    },
                    "foldingRange": {
                      "_vs_refreshSupport": true
                    },
                    "linkedEditingRange": {},
                    "semanticTokens": {
                      "requests": {
                        "range": true,
                        "full": {
                          "range": true
                        }
                      },
                      "tokenTypes": [
                        "namespace",
                        "type",
                        "class",
                        "enum",
                        "interface",
                        "struct",
                        "typeParameter",
                        "parameter",
                        "variable",
                        "property",
                        "enumMember",
                        "event",
                        "function",
                        "method",
                        "macro",
                        "keyword",
                        "modifier",
                        "comment",
                        "string",
                        "number",
                        "regexp",
                        "operator",
                        "cppMacro",
                        "cppEnumerator",
                        "cppGlobalVariable",
                        "cppLocalVariable",
                        "cppParameter",
                        "cppType",
                        "cppRefType",
                        "cppValueType",
                        "cppFunction",
                        "cppMemberFunction",
                        "cppMemberField",
                        "cppStaticMemberFunction",
                        "cppStaticMemberField",
                        "cppProperty",
                        "cppEvent",
                        "cppClassTemplate",
                        "cppGenericType",
                        "cppFunctionTemplate",
                        "cppNamespace",
                        "cppLabel",
                        "cppUserDefinedLiteralRaw",
                        "cppUserDefinedLiteralNumber",
                        "cppUserDefinedLiteralString",
                        "cppOperator",
                        "cppMemberOperator",
                        "cppNewDelete"
                      ],
                      "tokenModifiers": [
                        "declaration",
                        "definition",
                        "readonly",
                        "static",
                        "deprecated",
                        "abstract",
                        "async",
                        "modification",
                        "documentation",
                        "defaultLibrary"
                      ],
                      "formats": [
                        "relative"
                      ]
                    }
                  }
                }
                """;
            var serializer = new LspSerializer();
            serializer.RegisterRazorConverters();
            serializer.RegisterVSInternalExtensionConverters();
            var omniSharpCapabilities = serializer.DeserializeObject<OmniSharpClientCapabilities>(clientCapabilitiesJson);

            // Act
            var vsCapabilities = omniSharpCapabilities.ToVSClientCapabilities(serializer);

            // Assert
            var expectedCapabilitiesObj = serializer.DeserializeObject<VSInternalClientCapabilities>(clientCapabilitiesJson);
            var expectedCapabilities = JObject.FromObject(expectedCapabilitiesObj);
            var actualCapabilities = JObject.FromObject(vsCapabilities);

            AssertJTokensEqual(expectedCapabilities, actualCapabilities);
        }

        private static void AssertJTokensEqual(JToken expected, JObject actualClientCapabilities)
        {
            if (expected == null)
            {
                return;
            }

            if (expected.HasValues)
            {
                foreach (var token in expected)
                {
                    AssertJTokensEqual(token, actualClientCapabilities);
                }

                return;
            }

            // Token
            if (expected.Path.Contains("["))
            {
                // We're going to skip validating arrays
                return;
            }

            var pathParts = expected.Path.Split('.');
            JToken actual = actualClientCapabilities;
            for (var i = 0; i < pathParts.Length; i++)
            {
                actual = actual[pathParts[i]]!;
            }

            Assert.Equal(expected, actual);
        }
    }
}
