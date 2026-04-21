
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Tokito.Data;
using Tokito.Models;

public static class GameEndpointsMinimalApi
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Game").WithTags(nameof(Game));

        group.MapGet("/", async (GameStore db) =>
        {
            return await db.Games.ToListAsync();
        })
        .WithName("GetAllGames")
        .WithOpenApi();

        group.MapGet("/{id}", async Task<Results<Ok<Game>, NotFound>> (int gameid, GameStore db) =>
        {
            return await db.Games.AsNoTracking()
                .FirstOrDefaultAsync(model => model.GameId == gameid)
                is Game model
                    ? TypedResults.Ok(model)
                    : TypedResults.NotFound();
        })
        .WithName("GetGameById")
        .WithOpenApi();

        group.MapPut("/{id}", async Task<Results<Ok, NotFound>> (int gameid, Game game, GameStore db) =>
        {
            var affected = await db.Games
                .Where(model => model.GameId == gameid)
                .ExecuteUpdateAsync(setters => setters
                  .SetProperty(m => m.GameId, game.GameId)
                  .SetProperty(m => m.Name, game.Name)
                  .SetProperty(m => m.ReleaseDate, game.ReleaseDate)
                  .SetProperty(m => m.Rating, game.Rating)
                  .SetProperty(m => m.PublisherId, game.PublisherId)
                  .SetProperty(m => m.SystemRequirements, game.SystemRequirements)
                  .SetProperty(m => m.PublisherName, game.PublisherName)
                  );
            return affected == 1 ? TypedResults.Ok() : TypedResults.NotFound();
        })
        .WithName("UpdateGame")
        .WithOpenApi();

        group.MapPost("/", async (Game game, GameStore db) =>
        {
            db.Games.Add(game);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/Game/{game.GameId}", game);
        })
        .WithName("CreateGame")
        .WithOpenApi();

        group.MapDelete("/{id}", async Task<Results<Ok, NotFound>> (int gameid, GameStore db) =>
        {
            var affected = await db.Games
                .Where(model => model.GameId == gameid)
                .ExecuteDeleteAsync();
            return affected == 1 ? TypedResults.Ok() : TypedResults.NotFound();
        })
        .WithName("DeleteGame")
        .WithOpenApi();
    }
}