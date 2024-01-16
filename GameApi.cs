static partial class GameApi
{
    /// <summary>
    /// Creates a logger for the <see cref="GameApi"/> class.
    /// </summary>
    private static ILogger CreateLogger(this ILoggerFactory loggerFactory) => loggerFactory.CreateLogger(nameof(GameApi));

    /// <summary>
    /// Logs that a game was created.
    /// </summary>
    [LoggerMessage(LogLevel.Information, "Created game {GameId}", EventName = "CreateGame")]
    public static partial void LogCreatedGame(ILogger logger, string gameId);

    /// <summary>
    /// Maps the game API endpoints.
    /// </summary>
    public static RouteGroupBuilder MapGameApi(this IEndpointRouteBuilder routes)
    {
        // All games-related endpoints are under /games.
        var group = routes.MapGroup("/games");

        // Map handler for creating a new game.
        group.MapPost("/", (ILoggerFactory logFactory, GameRepository repo) =>
        {
            var game = repo.CreateGame();

            var logger = logFactory.CreateLogger();
            LogCreatedGame(logger, game.Id);

            return Results.Created($"/games/{game.Id}", game.Id);
        });

        group.MapGet("/{gameId}", (string gameId, GameRepository repo) => 
        {
            if (!repo.TryGetGame(gameId, out var game))
            {
                return Results.NotFound();
            }

            return Results.Ok();
        });

        group.MapPost("/games/{gameId}/broadcastStatus", async (string gameId, GameRepository repo) =>
        {
            if (!repo.TryGetGame(gameId, out var game))
            {
                return Results.NotFound();
            }

            if (game.Status != GameStatus.InProgress)
            {
                return Results.Forbid();
            }

            await game.BroadcastStatus();

            return Results.Ok();
        });


        return group;
    }
}