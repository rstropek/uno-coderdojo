using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

record Player(Game Game, WebSocket Socket, string Name, List<Card> Hand, int Score)
{
    public string PlayerId { get; } = Guid.NewGuid().ToString();

    public Player(Game Game, WebSocket Socket, string Name)
        : this(Game, Socket, Name, [], 0) { }

    public async Task ListeningLoop(ILogger log, GameRepository repo)
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
                    await RemovePlayer(log, repo);
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

        await RemovePlayer(log, repo);
        log.LogInformation("Listening loop for {Name} ({PlayerId}) has ended", Name, PlayerId);
    }

    private async Task RemovePlayer(ILogger log, GameRepository repo)
    {
        await Game.RemovePlayer(log, this);
        repo.CollectAbandonedGames();
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
            case nameof(StartMessage):
                if (Game.Status != GameStatus.WaitingForPlayers) { break; }
                if (Game.Players.Count < 2) 
                { 
                    await Game.BroadcastServerMessage("Das Spiel kann noch nicht gestartet werden, es müssen mindestens zwei SpielerInnen dabei sein.");
                    break;
                }
                if (Game.Host != this)
                {
                    await Game.BroadcastServerMessage($"{Name}, nur der Host kann das Spiel starten. {Game.Host?.Name}, {Name} möchte, dass das Spiel gestartet wird.");
                    break;
                }

                Game.StackOfCards = new Cards();
                foreach (var player in Game.Players)
                {
                    player.Hand.Clear();
                    for (var i = 0; i < 7; player.Hand.Add(Game.StackOfCards.Draw()), i++) ;
                }

                Game.CurrentPlayer = Game.Players[Random.Shared.Next(Game.Players.Count)];
                Game.Status = GameStatus.InProgress;
                Game.Direction = Direction.Up;

                Game.DiscardPile = new([Game.StackOfCards.Draw()]);

                await Game.BroadcastStatus();
                await Game.BroadcastServerMessage("Und los geht's, die Karten sind gemischt.");
                await Game.BroadcastServerMessage($"Als erstes ist {Game.CurrentPlayer.Name} dran, viel Glück 🍀!");
                break;
            case nameof(DropCard):
                if (Game.Status != GameStatus.InProgress) { break; }
                var dropCardMessage = JsonSerializer.Deserialize(message, MessagesSerializerContext.Default.DropCard);
                if (dropCardMessage is null) { return; }
                var cardToDrop = Hand.FirstOrDefault(h => h == dropCardMessage.Card);
                if (cardToDrop == null) { return; }
                if (Game.DiscardPile!.Peek().Color != cardToDrop.Color && Game.DiscardPile.Peek().Type != cardToDrop.Type)
                {
                    return;
                }

                Hand.Remove(cardToDrop);
                if (Hand.Count == 0)
                {
                    await Game.Broadcast(new WinnerMessage(PlayerId, Name), MessagesSerializerContext.Default.WinnerMessage);
                    await Game.BroadcastServerMessage($"Wir haben einen GEWINNER 🏆🥇: {this.Name}!");
                    Game.Status = GameStatus.Finished;
                    break;
                }

                Game.DiscardPile!.Push(cardToDrop);
                Game.CurrentPlayer = Game.Players[(Game.Players.IndexOf(this) + (int)Game.Direction) % Game.Players.Count];
                await Game.BroadcastStatus();
                await Game.BroadcastServerMessage($"{Name} hat eine {Card.CardTypeToString(cardToDrop.Type)} {Card.CardColorToString(cardToDrop.Color)} abgelegt.");
                await Game.BroadcastServerMessage($"Jetzt ist {Game.CurrentPlayer.Name} dran");
                break;
            case nameof(TakeFromPile):
                if (Game.Status != GameStatus.InProgress) { break; }
                var card = Game.StackOfCards!.Draw();
                Hand.Add(card);
                Game.CurrentPlayer = Game.Players[(Game.Players.IndexOf(this) + (int)Game.Direction) % Game.Players.Count];
                await Game.BroadcastStatus();
                await Game.BroadcastServerMessage($"{Name} nimmt eine Karte vom Stapel");
                await Game.BroadcastServerMessage($"Jetzt ist {Game.CurrentPlayer.Name} dran");
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
