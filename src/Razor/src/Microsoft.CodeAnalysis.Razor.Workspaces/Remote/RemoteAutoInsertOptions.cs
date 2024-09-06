using System.Runtime.Serialization;
using Microsoft.CodeAnalysis.Razor.Settings;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Remote;

[DataContract]
internal readonly record struct RemoteAutoInsertOptions
{
    [DataMember(Order = 0)]
    public bool EnableAutoClosingTags { get; init; } = true;

    [DataMember(Order = 1)]
    public bool FormatOnType { get; init; } = true;

    [DataMember(Order = 2)]
    public bool InsertSpaces { get; init; } = true;

    [DataMember(Order = 3)]
    public int TabSize { get; init; } = 4;

    public RemoteAutoInsertOptions()
    {
    }

    public static RemoteAutoInsertOptions From(ClientSettings clientSettings, FormattingOptions formattingOptions)
        => new()
        {
            EnableAutoClosingTags = clientSettings.AdvancedSettings.AutoClosingTags,
            FormatOnType = clientSettings.AdvancedSettings.FormatOnType,
            InsertSpaces = formattingOptions.InsertSpaces,
            TabSize = formattingOptions.TabSize
        };
}
