using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.Middleware;
using OrderManagement.Api.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] ({CorrelationId}) {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddOpenApi();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

    builder.Services.AddScoped<IOrderService, OrderService>();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
