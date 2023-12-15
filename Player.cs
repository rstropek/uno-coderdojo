using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

record Player(Game Game, WebSocket Socket, string Name, List<Card> Hand, int Score)
{
    public string PlayerId { get; } = Guid.NewGuid().ToString();

    public Player(Game Game, WebSocket Socket, string Name)
        : this(Game, Socket, Name, [], 0) { }

    public async Task ListeningLoop()
    {
        var buffer = new byte[1024 * 4];
        while (Socket.State == WebSocketState.Open)
        {
            var result = await Socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (Game.Players.Count == 0) { GameRepository.Games.Remove(Game.Id); }
                else
                { 
                    Game.Players.Remove(this);
                    await Game.BroadcastPlayerListChanged();
                }

                await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                return;
            }

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            await HandleMessage(message);
        }
    }

    public async Task HandleMessage(string message)
    {

        var typedMessage = JsonSerializer.Deserialize(message, MessagesSerializerContext.Default.Message);
        if (typedMessage is null) { return; }

        switch (typedMessage.Type)
        {
            case nameof(PublishMessage):
                var messageToPublish = JsonSerializer.Deserialize(message, MessagesSerializerContext.Default.PublishMessage);
                if (messageToPublish is null) { return; }
                await Game.BroadcastChatMessage(messageToPublish.Sender, messageToPublish.Message);
                break;
            default:
                break;
        }
    }

    public async Task SendStatus()
    {
        var playerStatus = GetPlayerStatus();
        var msgJson = JsonSerializer.Serialize(playerStatus, WebSocketsSerializerContext.Default.PlayerStatusDto);
        await Socket.SendAsync(Encoding.UTF8.GetBytes(msgJson), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task Send<T>(T message, JsonTypeInfo<T> type)
    {
        var msgJson = JsonSerializer.Serialize(message, type);
        await Socket.SendAsync(Encoding.UTF8.GetBytes(msgJson), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private PlayerStatusDto GetPlayerStatus()
    {
        return new PlayerStatusDto(
            Game.Status,
            [.. Hand],
            Game.DiscardPile?[1],
            Game.CurrentPlayer?.PlayerId,
            Game.Players
                .Where(player => player != this)
                .Select(player => new OtherPlayerStatusDto(player.PlayerId, player.Name, player.Hand.Count))
                .ToArray()
        );
    }
}

[JsonSerializable(typeof(PlayerStatusDto))]
partial class WebSocketsSerializerContext : JsonSerializerContext { }

record OtherPlayerStatusDto(
    string PlayerId,
    string Name,
    int NumberOfCardsInHand
);

record PlayerStatusDto(
    GameStatus GameStatus,
    Card[]? Hand,
    Card? DiscardPileTop,
    string? CurrentPlayerId,
    OtherPlayerStatusDto[]? OtherPlayers
);
