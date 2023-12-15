using System.Text.Json.Serialization;

record Message(string Type);

record PlayerListChanged(
    string[] PlayerList
) : Message(nameof(PlayerListChanged));

[JsonSerializable(typeof(PlayerListChanged))]
partial class MessagesSerializerContext : JsonSerializerContext { }
