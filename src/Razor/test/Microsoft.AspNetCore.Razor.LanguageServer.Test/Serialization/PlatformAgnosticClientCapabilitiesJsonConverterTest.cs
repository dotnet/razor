// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring.Test
{
    public class PlatformAgnosticClientCapabilitiesJsonConverterTest
    {
        [Fact]
        public void ReadJson_ReadsValues()
        {
            // Arrange
            // Note this is a small subset of the actual ClientCapabilities provided
            // for use in basic validations.
            var rawJson = @"{
  ""workspace"": {
    ""applyEdit"": true,
    ""workspaceEdit"": {
      ""documentChanges"": true
    }
  },
  ""textDocument"": {
    ""_vs_onAutoInsert"": {
      ""dynamicRegistration"": false
    },
    ""synchronization"": {
      ""willSave"": false,
      ""willSaveWaitUntil"": false,
      ""didSave"": true,
      ""dynamicRegistration"": false
    },
    ""completion"": {
      ""completionItem"": {
        ""snippetSupport"": false,
        ""commitCharactersSupport"": true
      },
      ""completionItemKind"": {
        ""valueSet"": [
          3
        ]
      },
      ""contextSupport"": false,
      ""dynamicRegistration"": false
    },
    ""hover"": {
      ""contentFormat"": [
        ""plaintext""
      ],
      ""dynamicRegistration"": false
    },
    ""signatureHelp"": {
      ""signatureInformation"": {
        ""documentationFormat"": [
          ""plaintext""
        ]
      },
      ""contextSupport"": true,
      ""dynamicRegistration"": false
    },
    ""codeAction"": {
      ""codeActionLiteralSupport"": {
        ""codeActionKind"": {
          ""valueSet"": [
            ""refactor.extract""
          ]
        }
      },
      ""dynamicRegistration"": false
    }
  }
}";
            var stringReader = new StringReader(rawJson);
            var serializer = new LspSerializer();
            serializer.RegisterRazorConverters();

            // Act
            var capabilities = serializer.JsonSerializer.Deserialize<PlatformAgnosticClientCapabilities>(new JsonTextReader(stringReader));

            // Assert
            Assert.True(capabilities.Workspace.ApplyEdit);
            Assert.Equal(MarkupKind.PlainText, capabilities.TextDocument.Hover.Value.ContentFormat.First());
            Assert.Equal(CompletionItemKind.Function, capabilities.TextDocument.Completion.Value.CompletionItemKind.ValueSet.First());
            Assert.Equal(MarkupKind.PlainText, capabilities.TextDocument.SignatureHelp.Value.SignatureInformation.DocumentationFormat.First());
            Assert.Equal(CodeActionKind.RefactorExtract, capabilities.TextDocument.CodeAction.Value.CodeActionLiteralSupport.CodeActionKind.ValueSet.First());
        }
    }
}
