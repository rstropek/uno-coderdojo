using System.Text.Json.Serialization;

record Message(string Type);

record PlayerListChanged(
    string[] PlayerList
) : Message(nameof(PlayerListChanged));

record PublishMessage(
    string Sender,
    string Message
) : Message(nameof(PublishMessage));

[JsonSerializable(typeof(PlayerListChanged))]
[JsonSerializable(typeof(PublishMessage))]
partial class MessagesSerializerContext : JsonSerializerContext { }
