using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json.Serialization.Metadata;

class Game(string id)
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    private async Task Broadcast<T>(T message, JsonTypeInfo<T> type)
    {
        foreach (var player in Players)
        {
            await player.Send(message, type);
        }
    }

    public Cards? StackOfCards { get; private set; }

    public Stack<Card>? DiscardPile { get; private set; }

    public Direction Direction { get; private set; } = Direction.Up;

    public GameStatus Status { get; private set; } = GameStatus.WaitingForPlayers;

    public Player? CurrentPlayer { get; private set; }

    private readonly object currentPlayerWriteLockObject = new();

    public Player? Host { get; private set; }

    private readonly List<Player> PlayersList = [];

    public IReadOnlyList<Player> Players => PlayersList;

    private readonly object playersWriteLockObject = new();

    public string Id { get; } = id;

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

    public async Task RemovePlayer(ILogger log, Player playerToRemove)
    {
        lock (playersWriteLockObject)
        {
            if (PlayersList.Remove(playerToRemove))
            {
                lock (currentPlayerWriteLockObject)
                {
                    if (CurrentPlayer == playerToRemove && Players.Count > 0)
                    {
                        CurrentPlayer = Players[(PlayersList.IndexOf(playerToRemove) + (int)Game.Direction) % Game.Players.Count];
                    }
                }

                log.LogInformation("Removed player {Name} ({PlayerId})", Name, PlayerId);
                await Game.BroadcastPlayerListChanged();
                if (Game.Status == GameStatus.InProgress)
                {
                    await Game.BroadcastStatus();
                }
            }

            if (Socket.State == WebSocketState.Open)
            {
                await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
        }
    }

}