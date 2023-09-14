
using Blazored.LocalStorage;
using Google_Like_Blazor.Data;

using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Google_Like_Blazor;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.Configure<ConnectionStringModel>(
        builder.Configuration.GetSection("MongoDatabase"));
        builder.Services.Configure<RedisConfig>(
            builder.Configuration.GetSection("RedisConfig"));
        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddFileReaderService();
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<MemoryStorageUtility>();
        builder.Services.AddSingleton<MemoryCacheConfig>();
        builder.Services.AddScoped<RepositoryCache>();


        builder.Services.AddSingleton<MyRedisCache>();
        builder.Services.AddBlazoredLocalStorage();

        // Add services to the container.
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddSingleton<WeatherForecastService>();
        builder.Services.AddScoped<IFileRepo, FileService>();
        builder.Services.AddScoped<IGridSfRepo, GridSfService>();
        builder.Host.UseSerilog((ctx, lc) =>
     lc.WriteTo.Console().ReadFrom.Configuration(ctx.Configuration));
        builder.Services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                  new[] { "application/octet-stream" });
        });
        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
            config.SnackbarConfiguration.PreventDuplicates = false;
            config.SnackbarConfiguration.NewestOnTop = false;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 2000;
            config.SnackbarConfiguration.HideTransitionDuration = 500;
            config.SnackbarConfiguration.ShowTransitionDuration = 500;
            config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
        });
        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();

        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        });

        app.UseHttpsRedirection();

        app.UseStaticFiles();

        app.UseRouting();

        app.UseEndpoints(e =>
        {

            e.MapBlazorHub();

            e.MapControllers();

            e.MapFallbackToPage("/_Host");

        });

        app.Run();
    }
}

