using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

record Player(Game Game, WebSocket Socket, string Name, List<Card> Hand, int Score)
{
    public string PlayerId { get; } = Guid.NewGuid().ToString();

    public Player(Game Game, WebSocket Socket, string Name)
        : this(Game, Socket, Name, [], 0) { }

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
                    await RemovePlayer(log);
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

        await RemovePlayer(log);
        log.LogInformation("Listening loop for {Name} ({PlayerId}) has ended", Name, PlayerId);
    }

    private async Task RemovePlayer(ILogger log)
    {
        if (Game.Players.Count == 0 && GameRepository.Games.Remove(Game.Id))
        {
            log.LogInformation("Removed game {GameId} because no players are left", Game.Id);
        }
        else if (Game.Players.Remove(this))
        {
            log.LogInformation("Removed player {Name} ({PlayerId})", Name, PlayerId);
            await Game.BroadcastPlayerListChanged();
        }

        if (Socket.State == WebSocketState.Open)
        {
            await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
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
            case nameof(DropCard):
                var dropCardMessage = JsonSerializer.Deserialize(message, MessagesSerializerContext.Default.DropCard);
                if (dropCardMessage is null) { return; }
                var cardToDrop = Hand.FirstOrDefault(h => h == dropCardMessage.Card);
                if (cardToDrop == null) { return; }
                if (Game.DiscardPile!.Peek().Color != cardToDrop.Color && Game.DiscardPile.Peek().Type != cardToDrop.Type)
                {
                    return;
                }
                Hand.Remove(cardToDrop);
                Game.DiscardPile!.Push(cardToDrop);
                Game.CurrentPlayer = Game.Players[(Game.Players.IndexOf(this) + (int)Game.Direction) % Game.Players.Count];
                await Game.BroadcastStatus();
                await Game.BroadcastServerMessage($"{Name} dropped a {cardToDrop.Color} {cardToDrop.Type}");
                await Game.BroadcastServerMessage($"It is now {Game.CurrentPlayer.Name}'s turn");
                break;
            case nameof(TakeFromPile):
                var card = Game.StackOfCards!.Draw();
                Hand.Add(card);
                Game.CurrentPlayer = Game.Players[(Game.Players.IndexOf(this) + (int)Game.Direction) % Game.Players.Count];
                await Game.BroadcastStatus();
                await Game.BroadcastServerMessage($"{Name} took a card from the pile");
                await Game.BroadcastServerMessage($"It is now {Game.CurrentPlayer.Name}'s turn");
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
            Game.DiscardPile!.Peek(),
            Game.CurrentPlayer?.PlayerId,
            Game.CurrentPlayer!.PlayerId == PlayerId,
            Game.Players
                .Where(player => player != this)
                .Select(player => new OtherPlayerStatusMessage(player.PlayerId, player.Name, player.Hand.Count))
                .ToArray()
        );
    }
}
