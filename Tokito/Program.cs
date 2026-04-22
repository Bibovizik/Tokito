using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tokito.Data;
using Tokito.Mappers;
using Tokito.Models;
using Tokito.Services.Auth;
using Tokito.Services.GameImages;
using Tokito.Services.GameReviews;
using Tokito.Services.Games;
using Tokito.Services.Markets;
using Tokito.Services.Pricing;
using Tokito.Services.Wallets;

namespace Tokito
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddAutoMapper(cfg => { }, typeof(GameProfile));
            builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<GameStore>()
            .AddDefaultTokenProviders()
            .AddRoles<IdentityRole<int>>();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.Path = "/";
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.Name = "Tokito-Identity";

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };

                options.Events.OnValidatePrincipal = async context =>
                {
                    var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!int.TryParse(userIdValue, out var userId))
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync();
                        return;
                    }

                    var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
                    var currentUser = await userManager.FindByIdAsync(userId.ToString());
                    if (currentUser == null || currentUser.AccountStatus == UserAccountStatusValues.Blocked)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync();
                    }
                };
            });


            builder.Services.AddDbContext<GameStore>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp",
                    policy =>
                    {
                        policy.WithOrigins(
                            "http://localhost:3000",
                            "https://localhost:3000",
                            "http://localhost:5173",
                            "https://localhost:5173"
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                    });
            });

            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IGameImageStorage, GameImageStorage>();
            builder.Services.AddScoped<IGameService, GameService>();
            builder.Services.AddScoped<IMarketResolver, MarketResolver>();
            builder.Services.AddScoped<IWalletService, WalletService>();
            builder.Services.AddScoped<IGameReviewService, GameReviewService>();
            builder.Services.AddHttpClient<INbuExchangeRateService, NbuExchangeRateService>(client =>
            {
                client.BaseAddress = new Uri("https://bank.gov.ua/");
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var gameStore = scope.ServiceProvider.GetRequiredService<GameStore>();
                await gameStore.Database.MigrateAsync();

                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
                string[] roles = { "Admin", "User", "Publisher" };

                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole<int>(role));
                    }
                }

                await PricingMarketSeeder.SeedAsync(gameStore);
            }


            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.EnablePersistAuthorization();
                });
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCors("AllowReactApp");

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
