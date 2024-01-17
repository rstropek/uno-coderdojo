using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;

partial class GameRepository(ILogger<GameRepository> logger, GameFactory gameFactory)
{
    private readonly ConcurrentDictionary<string, Game> Games = [];

    public Game CreateGame()
    {
        var game = gameFactory.CreateGame();
        Debug.Assert(Games.TryAdd(game.Id, game));
        return game;
    }

    public bool TryGetGame(string id, out Game? game) => Games.TryGetValue(id, out game);

    public bool TryRemove(string id) => Games.TryRemove(id, out _);

    public void CollectAbandonedGames()
    {
        foreach (var game in Games.Values)
        {
            if (game.Players.Count == 0)
            {
                if (TryRemove(game.Id))
                {
                    LogPlayerNotFound(logger, game.Id);
                }
            }
        }
    }

    public void EndAllConnections()
    {
        foreach (var game in Games.Values)
        {
            game.EndAllConnections();
        }
    }

    [LoggerMessage(LogLevel.Information, "Removed game {gameId} because it was abandoned", EventName = "RemovedGame")]
    public static partial void LogPlayerNotFound(ILogger logger, string gameId);
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


