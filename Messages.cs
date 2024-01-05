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
    bool ItIsYourTurn,
    OtherPlayerStatusMessage[]? OtherPlayers
) : Message(nameof(PlayerStatusMessage));

record Ping() : Message(nameof(Ping));

record DropCard(
    Card Card
) : Message(nameof(DropCard));

record TakeFromPile() : Message(nameof(TakeFromPile));

[JsonSerializable(typeof(PlayerListChanged))]
[JsonSerializable(typeof(PublishMessage))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(PlayerStatusMessage))]
[JsonSerializable(typeof(Ping))]
[JsonSerializable(typeof(DropCard))]
[JsonSerializable(typeof(TakeFromPile))]
partial class MessagesSerializerContext : JsonSerializerContext { }
