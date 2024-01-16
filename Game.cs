using System.Net.WebSockets;
using System.Text.Json.Serialization.Metadata;

partial class Game(ILogger logger)
{
    private static readonly char[] vowels = ['a', 'e', 'i', 'o', 'u'];
    private static readonly char[] consonants = [ 'b', 'c', 'd', 'f', 'g', 'h', 'j', 'k', 'l',
                'm', 'n', 'p', 'q', 'r', 's', 't', 'v', 'w', 'x', 'y', 'z' ];

    private static string CreateGameName()
    {
        return string.Create(6, (object)null!, (buffer, _) =>
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = i % 2 == 0
                    ? vowels[Random.Shared.Next(vowels.Length)]
                    : consonants[Random.Shared.Next(consonants.Length)];
            }
        });
    }

    private async Task Broadcast<T>(T message, JsonTypeInfo<T> type)
    {
        foreach (var player in Players)
        {
            await player.Send(message, type);
        }
    }

    // Whenever ANYTHING related to the game is changed,
    // we need to do the changes inside a lock on this object.
    private readonly object gameWriteLock = new();

    private readonly List<Player> PlayersList = [];

    private Cards? StackOfCards { get; set; }

    private Stack<Card>? DiscardPile { get; set; }

    public Direction Direction { get; private set; } = Direction.Up;

    public GameStatus Status { get; private set; } = GameStatus.WaitingForPlayers;

    public Player? CurrentPlayer { get; private set; }

    public Player? Host { get; private set; }

    public IReadOnlyList<Player> Players => PlayersList;

    public string Id { get; } = CreateGameName();

    public async Task BroadcastStatus()
    {
        foreach (var player in Players)
        {
            await player.SendStatus();
        }
    }

    public async Task BroadcastServerMessage(string message)
    {
        var msg = new PublishMessage("🤖", message);
        await Broadcast(msg, MessagesSerializerContext.Default.PublishMessage);
    }

    public async Task BroadcastPlayerListChanged()
    {
        var msg = new PlayerListChanged(Players.Select(player => player.Name).ToArray());
        await Broadcast(msg, MessagesSerializerContext.Default.PlayerListChanged);
    }

    public async Task BroadcastChatMessage(string from, string message)
    {
        var msg = new PublishMessage(from, message);
        await Broadcast(msg, MessagesSerializerContext.Default.PublishMessage);
    }

    private void MoveToNextPlayer()
    {
        // This method assumes that we are already inside a lock on gameWriteLock

        if (Players.Count == 0 || CurrentPlayer == null)
        {
            return;
        }

        var nextPlayerIndex = (PlayersList.IndexOf(CurrentPlayer) + (int)Direction) % Players.Count;
        CurrentPlayer = Players[nextPlayerIndex];
    }

    public async Task AddPlayer(Player player)
    {
        lock (gameWriteLock)
        {
            // First player is always the host
            if (Players.Count == 0) { Host = player; }
            PlayersList.Add(player);
        }

        await BroadcastPlayerListChanged();
    }

    public async Task RemovePlayer(Player playerToRemove)
    {
        lock (gameWriteLock)
        {
            if (PlayersList.Contains(playerToRemove))
            {
                if (CurrentPlayer == playerToRemove) { MoveToNextPlayer(); }
                PlayersList.Remove(playerToRemove);
                LogRemovedPlayer(logger, playerToRemove.Name, playerToRemove.PlayerId);
            }
            else
            {
                return;
            }
        }

        // Notify all players that the player list has changed
        await BroadcastPlayerListChanged();
        if (Status == GameStatus.InProgress) { await BroadcastStatus(); }
    }

    public async Task DropCard(Player player, Card card)
    {
        lock (gameWriteLock)
        {
            if (Status != GameStatus.InProgress)
            {
                LogGameFlowInconsistency(logger, player.PlayerId, "Trying to drop a card when the game is not in progress");
                return;
            }

            var cardToDrop = player.Hand.FirstOrDefault(h => h == card);
            if (cardToDrop == null)
            {
                LogGameFlowInconsistency(logger, player.PlayerId, "Trying to drop a card that is not in the player's hand");
                return;
            }

            if (DiscardPile!.TryPeek(out var c) && c.Color != cardToDrop.Color && c.Type != cardToDrop.Type)
            {
                LogGameFlowInconsistency(logger, player.PlayerId, "Trying to drop a card that does not match the discard pile");
                return;
            }

            player.Hand.Remove(cardToDrop);
            MoveToNextPlayer();
            DiscardPile!.Push(card);
        }

        await CheckForWinner();
        await BroadcastStatus();
        await BroadcastServerMessage($"{player.Name} hat eine {Card.CardTypeToString(card.Type)} {Card.CardColorToString(card.Color)} abgelegt.");
        await BroadcastServerMessage($"Jetzt ist {CurrentPlayer!.Name} dran");
    }

    public async Task TakeFromPile(Player player)
    {
        lock (gameWriteLock)
        {
            if (Status != GameStatus.InProgress)
            {
                LogGameFlowInconsistency(logger, player.PlayerId, "Trying to take a card when the game is not in progress");
                return;
            }

            var card = StackOfCards!.Draw();
            player.Hand.Add(card);

            MoveToNextPlayer();
        }

        await BroadcastStatus();
        await BroadcastServerMessage($"{player.Name} nimmt eine Karte vom Stapel");
        await BroadcastServerMessage($"Jetzt ist {CurrentPlayer!.Name} dran");
    }

    public async Task Start(Player starter)
    {
        if (Host != starter)
        {
            await BroadcastServerMessage($"{starter.Name}, nur der Host kann das Spiel starten. {Host?.Name}, {starter.Name} möchte, dass das Spiel gestartet wird.");
            return;
        }

        if (Players.Count < 2)
        {
            await BroadcastServerMessage("Das Spiel kann noch nicht gestartet werden, es müssen mindestens zwei SpielerInnen dabei sein.");
            return;
        }

        lock (gameWriteLock)
        {
            if (Status != GameStatus.WaitingForPlayers)
            {
                LogDoubleStart(logger, Id);
                return;
            }

            // Double-check because only now we are inside the lock
            if (Host != starter || Players.Count < 2) { return; }

            StackOfCards = new Cards();
            foreach (var player in Players)
            {
                player.Hand.Clear();
                for (var i = 0; i < 7; player.Hand.Add(StackOfCards.Draw()), i++) ;
            }

            CurrentPlayer = Players[Random.Shared.Next(Players.Count)];
            Status = GameStatus.InProgress;
            Direction = Direction.Up;

            DiscardPile = new([StackOfCards.Draw()]);
        }

        await BroadcastStatus();
        await BroadcastServerMessage("Und los geht's, die Karten sind gemischt.");
        await BroadcastServerMessage($"Als erstes ist {CurrentPlayer.Name} dran, viel Glück 🍀!");
    }

    public async Task CheckForWinner()
    {
        Player? winner;
        lock (gameWriteLock)
        {
            winner = Players.FirstOrDefault(player => player.Hand.Count == 0);
            if (winner == null) { return; }
            Status = GameStatus.Finished;
        }

        await Broadcast(new WinnerMessage(winner.PlayerId, winner.Name), MessagesSerializerContext.Default.WinnerMessage);
        await BroadcastServerMessage($"Wir haben einen GEWINNER 🏆🥇: {winner.Name}!");
    }

    public void EndAllConnections()
    {
        foreach (var player in Players)
        {
            player.Socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure, 
                "Server is shutting down", 
                CancellationToken.None).Wait();
        }
    }

    public Card? PeekDiscardPile() => DiscardPile?.Peek();

    [LoggerMessage(LogLevel.Information, "Removed player {Name} ({PlayerId})", EventName = "RemovedPlayer")]
    public static partial void LogRemovedPlayer(ILogger logger, string name, string playerId);

    [LoggerMessage(LogLevel.Warning, "Trying to start a game ({gameId}) that has already been started", EventName = "DoubleStart")]
    public static partial void LogDoubleStart(ILogger logger, string gameId);

    [LoggerMessage(LogLevel.Warning, "Game flow inconsistency (player {playerId}): {message}", EventName = "GameFlowInconsistency")]
    public static partial void LogGameFlowInconsistency(ILogger logger, string playerId, string message);
}

class GameFactory(ILogger<Game> logger)
{
    public Game CreateGame() => new(logger);
}
