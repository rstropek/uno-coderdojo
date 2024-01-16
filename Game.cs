using System.Text.Json.Serialization.Metadata;

class Game
{
    private static readonly char[] vowels = ['a', 'e', 'i', 'o', 'u'];
    private static readonly char[] consonants = [ 'b', 'c', 'd', 'f', 'g', 'h', 'j', 'k', 'l',
                'm', 'n', 'p', 'q', 'r', 's', 't', 'v', 'w', 'x', 'y', 'z' ];

    private async Task Broadcast<T>(T message, JsonTypeInfo<T> type)
    {
        foreach (var player in Players)
        {
            await player.Send(message, type);
        }
    }

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

    private readonly object gameWriteLock = new();

    private readonly List<Player> PlayersList = [];

    public Cards? StackOfCards { get; private set; }

    public Cards? DiscardPile { get; private set; }

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
        if (Players.Count == 0 || CurrentPlayer == null)
        {
            return;
        }

        var nextPlayerIndex = (PlayersList.IndexOf(CurrentPlayer) + (int)Direction) % Players.Count;
        CurrentPlayer = Players[nextPlayerIndex];
    }

    public async Task RemovePlayer(ILogger log, Player playerToRemove)
    {
        lock (gameWriteLock)
        {
            if (PlayersList.Contains(playerToRemove))
            {
                if (CurrentPlayer == playerToRemove) { MoveToNextPlayer(); }
                PlayersList.Remove(playerToRemove);
                LogRemovedPlayer(log, playerToRemove.Name, playerToRemove.PlayerId);
            }
            else
            {
                // This should never happen -> warning
                LogPlayerNotFound(log, playerToRemove.Name, playerToRemove.PlayerId);
            }
        }

        // Notify all players that the player list has changed
        await BroadcastPlayerListChanged();
        if (Status == GameStatus.InProgress) { await BroadcastStatus(); }
    }

    [LoggerMessage(LogLevel.Information, "Removed player {Name} ({PlayerId})", EventName = "RemovedPlayer")]
    public static partial void LogRemovedPlayer(ILogger logger, string name, string playerId);

    [LoggerMessage(LogLevel.Warning, "Could not find player {Name} ({PlayerId}) to remove", EventName = "PlayerNotFound")]
    public static partial void LogPlayerNotFound(ILogger logger, string name, string playerId);

}