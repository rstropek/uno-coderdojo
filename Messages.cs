using System.Text.Json.Serialization;

record Message(string Type);

record PlayerListChanged(
    string[] PlayerList
) : Message(nameof(PlayerListChanged));

record PublishMessage(
    string Sender,
    string Message
) : Message(nameof(PublishMessage));

record OtherPlayerStatusMessage(
    string PlayerId,
    string Name,
    int NumberOfCardsInHand
);

record PlayerStatusMessage(
    GameStatus GameStatus,
    Card[]? Hand,
    Card? DiscardPileTop,
    string? CurrentPlayerId,
    OtherPlayerStatusMessage[]? OtherPlayers
) : Message(nameof(PlayerStatusMessage));

record Ping() : Message(nameof(Ping));

[JsonSerializable(typeof(PlayerListChanged))]
[JsonSerializable(typeof(PublishMessage))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(PlayerStatusMessage))]
[JsonSerializable(typeof(Ping))]
partial class MessagesSerializerContext : JsonSerializerContext { }
