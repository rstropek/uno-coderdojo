using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

record Player(Game Game, WebSocket Socket, string Name)
{
    public string PlayerId { get; } = Guid.NewGuid().ToString();

    public List<Card> Hand { get; } = [];

    public async Task ListeningLoop(ILogger log)
    {
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                Thread.Sleep(5000);
                if (Socket.State != WebSocketState.Open)
                {
                    cts.Cancel();
                    await RemovePlayer();
                    return;
                }

                await Send(new Ping(), MessagesSerializerContext.Default.Ping);
            }
        });

        var buffer = new byte[1024 * 4];
        while (Socket.State == WebSocketState.Open)
        {
            try
            {
                var result = await Socket.ReceiveAsync(buffer, cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) { break; }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await HandleMessage(message);
            }
            catch (OperationCanceledException) { }
        }

        await RemovePlayer();
        log.LogInformation("Listening loop for {Name} ({PlayerId}) has ended", Name, PlayerId);
    }

    private async Task RemovePlayer()
    {
        await Game.RemovePlayer(this);
        if (Socket.State == WebSocketState.Open)
        {
            await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
    }

    private async Task HandleMessage(string message)
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
            case nameof(StartMessage):
                await Game.Start(this);
                break;
            case nameof(DropCard):
                var dropCardMessage = JsonSerializer.Deserialize(message, MessagesSerializerContext.Default.DropCard);
                if (dropCardMessage is null) { return; }
                await Game.DropCard(this, dropCardMessage.Card);
                break;
            case nameof(TakeFromPile):
                await Game.TakeFromPile(this);
                break;
            default:
                break;
        }
    }

    public async Task SendStatus()
    {
        var playerStatus = GetPlayerStatus();
        await Send(playerStatus, MessagesSerializerContext.Default.PlayerStatusMessage);
    }

    public async Task Send<T>(T message, JsonTypeInfo<T> type)
    {
        var msgJson = JsonSerializer.Serialize(message, type);
        await Socket.SendAsync(Encoding.UTF8.GetBytes(msgJson), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private PlayerStatusMessage GetPlayerStatus()
    {
        return new PlayerStatusMessage(
            Game.Status,
            [.. Hand],
            Game.PeekDiscardPile(),
            Game.CurrentPlayer?.PlayerId,
            Game.CurrentPlayer!.PlayerId == PlayerId,
            Game.Players
                .Where(player => player != this)
                .Select(player => new OtherPlayerStatusMessage(player.PlayerId, player.Name, player.Hand.Count))
                .ToArray()
        );
    }
}
