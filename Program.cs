using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        AppJsonSerializerContext.Default,
        CardSerializerContext.Default
    );
});
var app = builder.Build();

app.UseWebSockets();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

app.MapGet("/", () => "Hello World!");

app.MapPost("/games", (ILogger<Program> log) =>
{
    var game = GameRepository.CreateGame();

    log.LogInformation("Created game {GameId}", game.Id);
    return Results.Created($"/games/{game.Id}", game.Id);
});

app.MapGet("/games/{gameId}", (string gameId) => 
{
    if (!GameRepository.Games.TryGetValue(gameId, out var game))
    {
        return Results.NotFound();
    }

    return Results.Ok();
});

app.MapPost("/games/{gameId}/start", async (string gameId) =>
{
    if (!GameRepository.Games.TryGetValue(gameId, out var game))
    {
        return Results.NotFound();
    }

    if (game.Status != GameStatus.WaitingForPlayers)
    {
        return Results.Forbid();
    }

    if (game.Players.Count < 2)
    {
        return Results.BadRequest();
    }

    game.StackOfCards = new Cards();
    foreach (var player in game.Players)
    {
        player.Hand.Clear();
        for (var i = 0; i < 7; player.Hand.Add(game.StackOfCards.Draw()), i++) ;
    }

    game.CurrentPlayer = game.Players[Random.Shared.Next(game.Players.Count)];
    game.Status = GameStatus.InProgress;
    game.Direction = Direction.Up;

    game.DiscardPile = new([game.StackOfCards.Draw()]);

    await game.BroadcastStatus();
    await game.BroadcastServerMessage("Und los geht's, die Karten sind gemischt.");
    await game.BroadcastServerMessage($"Als erstes ist {game.CurrentPlayer.Name} dran, viel Glück 🍀!");

    return Results.Ok();
});

app.MapPost("/games/{gameId}/broadcastStatus", async (string gameId) =>
{
    if (!GameRepository.Games.TryGetValue(gameId, out var game))
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

app.Use(async (HttpContext context, RequestDelegate next) =>
{
    var match = RegularExpressions.JoinPath().Match(context.Request.Path);
    if (match.Success && context.WebSockets.IsWebSocketRequest)
    {
        if (!GameRepository.Games.TryGetValue(match.Groups[1].Value, out Game? game))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (game.Status != GameStatus.WaitingForPlayers)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!context.Request.Query.TryGetValue("name", out var name))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var websocket = await context.WebSockets.AcceptWebSocketAsync();
        var player = new Player(game, websocket, name!);
        game.Players.Add(player);
        await game.BroadcastPlayerListChanged();

        var log = app.Services.GetRequiredService<ILogger<Program>>();
        await player.ListeningLoop(log);
        return;
    }

    await next(context);
});

app.Run();

[JsonSerializable(typeof(string))]
partial class AppJsonSerializerContext : JsonSerializerContext { }

static partial class RegularExpressions
{
    [GeneratedRegex(@"^/games/([a-z]{6})/join$", RegexOptions.IgnoreCase, "en-US")]
    public static partial Regex JoinPath();
}
