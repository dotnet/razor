//// Copyright (c) .NET Foundation. All rights reserved.
//// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

//using System;
//using System.Collections.Generic;
//using Microsoft.VisualStudio.LanguageServer.Protocol;
//using OmniSharpFormattingOptions = OmniSharp.Extensions.LanguageServer.Protocol.Models.FormattingOptions;
//using OmniSharpPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
//using OmniSharpRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
//using OmniSharpTextEdit = OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit;

//namespace Microsoft.VisualStudio.LanguageServerClient.Razor
//{
//    internal static class ProtocolExtensions
//    {
//        public static Position ToLSPPosition(this OmniSharpPosition position)
//        {
//            if (position is null)
//            {
//                throw new ArgumentNullException(nameof(position));
//            }

//            return new Position((int)position.Line, (int)position.Character);
//        }

//        public static Range ToLSPRange(this OmniSharpRange range)
//        {
//            if (range is null)
//            {
//                throw new ArgumentNullException(nameof(range));
//            }

//            return new Range()
//            {
//                Start = range.Start.ToLSPPosition(),
//                End = range.End.ToLSPPosition()
//            };
//        }

//        public static TextEdit ToLSPTextEdit(this OmniSharpTextEdit edit)
//        {
//            if (edit is null)
//            {
//                throw new ArgumentNullException(nameof(edit));
//            }

//            return new TextEdit()
//            {
//                Range = edit.Range.ToLSPRange(),
//                NewText = edit.NewText
//            };
//        }

//        public static OmniSharpPosition ToOmniSharpPosition(this Position position)
//        {
//            if (position is null)
//            {
//                throw new ArgumentNullException(nameof(position));
//            }

//            return new OmniSharpPosition(position.Line, position.Character);
//        }

//        public static OmniSharpRange ToOmniSharpRange(this Range range)
//        {
//            if (range is null)
//            {
//                throw new ArgumentNullException(nameof(range));
//            }

//            return new OmniSharpRange()
//            {
//                Start = range.Start.ToOmniSharpPosition(),
//                End = range.End.ToOmniSharpPosition()
//            };
//        }

//        public static OmniSharpTextEdit ToOmniSharpTextEdit(this TextEdit edit)
//        {
//            if (edit is null)
//            {
//                throw new ArgumentNullException(nameof(edit));
//            }

//            return new OmniSharpTextEdit()
//            {
//                Range = edit.Range.ToOmniSharpRange(),
//                NewText = edit.NewText
//            };
//        }

//        public static FormattingOptions ToLSPFormattingOptions(this OmniSharpFormattingOptions options)
//        {
//            if (options is null)
//            {
//                throw new ArgumentNullException(nameof(options));
//            }

//            var otherOptions = new Dictionary<string, object>();
//            foreach (var kvp in options)
//            {
//                var value = kvp.Value;
//                if (value.IsBool)
//                {
//                    otherOptions.Add(kvp.Key, value.Bool);
//                }
//                else if (value.IsString)
//                {
//                    otherOptions.Add(kvp.Key, value.String);
//                }
//                else if (value.IsLong)
//                {
//                    otherOptions.Add(kvp.Key, (int)value.Long);
//                }
//                else
//                {
//                    otherOptions.Add(kvp.Key, value);
//                }
//            }

//            return new FormattingOptions()
//            {
//                TabSize = (int)options.TabSize,
//                InsertSpaces = options.InsertSpaces,
//                OtherOptions = otherOptions
//            };
//        }
//    }
//}
