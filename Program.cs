using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
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

app.MapPost("/games/{gameId}/start", (string gameId) =>
{
    if (!GameRepository.Games.TryGetValue(gameId, out var game))
    {
        return Results.NotFound();
    }

    if (game.Status != GameStatus.WaitingForPlayers)
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

    game.DiscardPile = [game.StackOfCards.Draw()];
    return Results.Ok();
});

app.Use((Func<HttpContext, Func<Task>, Task>)(async (HttpContext context, Func<Task> next) =>
{
    var match = RegularExpressions.JoinPath().Match(context.Request.Path);
    if (match.Success)
    {
        if (context.WebSockets.IsWebSocketRequest
            && GameRepository.Games.TryGetValue(match.Groups[1].Value, out Game? game)
            && context.Request.Query.TryGetValue("name", out var name))
        {
            var websocket = await context.WebSockets.AcceptWebSocketAsync();
            var player = new Player(game, websocket, name!);
            game.Players.Add(player);
            await game.BroadcastPlayerListChanged();

            await player.ListeningLoop();
        }
    }
    await next();
}));

app.Run();

[JsonSerializable(typeof(string))]
partial class AppJsonSerializerContext : JsonSerializerContext { }

static partial class RegularExpressions
{
    [GeneratedRegex(@"^/games/([a-z]{6})/join$", RegexOptions.IgnoreCase, "en-US")]
    public static partial Regex JoinPath();
}
