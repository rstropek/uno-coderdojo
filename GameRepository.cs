using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

static class GameRepository
{
    private static readonly char[] vowels = ['a', 'e', 'i', 'o', 'u'];
    private static readonly char[] consonants = [ 'b', 'c', 'd', 'f', 'g', 'h', 'j', 'k', 'l',
                'm', 'n', 'p', 'q', 'r', 's', 't', 'v', 'w', 'x', 'y', 'z' ];

    public static readonly Dictionary<string, Game> Games = [];

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

    public static Game CreateGame()
    {
        var game = new Game(CreateGameName());
        Games.Add(game.Id, game);
        return game;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<GameStatus>))]
enum GameStatus
{
    NotStarted,
    WaitingForPlayers,
    InProgress,
    Finished
}

enum Direction
{
    Down = -1,
    Up = 1
}

record Game(string Id, List<Player> Players)
{
    public Cards? StackOfCards { get; set; }

    public Card[]? DiscardPile { get; set; }

    public Direction Direction { get; set; } = Direction.Up;

    public GameStatus Status { get; set; } = GameStatus.WaitingForPlayers;

    public Player? CurrentPlayer { get; set; }

    public Game(string id) : this(id, []) { }

    public async Task BroadcastStatus()
    {
        foreach (var player in Players)
        {
            await player.SendStatus();
        }
    }

    public async Task Broadcast<T>(T message, JsonTypeInfo<T> type)
    {
        foreach (var player in Players)
        {
            await player.Send(message, type);
        }
    }

    public async Task BroadcastPlayerListChanged()
    {
        var msg = new PlayerListChanged(Players.Select<Player, string>(player => player.Name).ToArray());
        foreach (var otherPlayer in Players)
        {
            await otherPlayer.Send(msg, (JsonTypeInfo<PlayerListChanged>)MessagesSerializerContext.Default.PlayerListChanged);
        }
    }
}