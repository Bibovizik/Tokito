
using AutoMapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tokito.Data;
using Tokito.Mappers;
using Tokito.Models;
using Tokito.Services.Auth;
using Tokito.Services.Games;
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
            builder.Services.AddScoped<IGameService, GameService>();
            builder.Services.AddScoped<IWalletService, WalletService>();
            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
                string[] roles = { "Admin", "User", "Publisher" };

                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole<int>(role));
                    }
                }
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
            app.UseCors("AllowReactApp");

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.UseStaticFiles();
            app.Run();
        }
    }
}
