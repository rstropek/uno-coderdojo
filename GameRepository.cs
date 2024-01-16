using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

class GameRepository
{
    private static readonly char[] vowels = ['a', 'e', 'i', 'o', 'u'];
    private static readonly char[] consonants = [ 'b', 'c', 'd', 'f', 'g', 'h', 'j', 'k', 'l',
                'm', 'n', 'p', 'q', 'r', 's', 't', 'v', 'w', 'x', 'y', 'z' ];

    private readonly ConcurrentDictionary<string, Game> Games = [];

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

    public Game CreateGame()
    {
        var game = new Game(CreateGameName());
        Debug.Assert(Games.TryAdd(game.Id, game));
        return game;
    }

    public bool TryGetGame(string id, out Game? game) => Games.TryGetValue(id, out game);

    public bool TryRemove(string id) => Games.TryRemove(id, out _);
}

[JsonConverter(typeof(JsonStringEnumConverter<GameStatus>))]
enum GameStatus
{
    NotStarted,
    WaitingForPlayers,
    InProgress,
    Finished
}

[JsonConverter(typeof(JsonStringEnumConverter<Direction>))]
enum Direction
{
    Down = -1,
    Up = 1
}


