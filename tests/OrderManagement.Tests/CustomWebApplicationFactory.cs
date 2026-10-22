using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderManagement.Api.Data;

namespace OrderManagement.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ordermgmt-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath};Cache=Shared;Default Timeout=30"));
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_dbPath};Cache=Shared;Default Timeout=30"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (var path in new[] { _dbPath, _dbPath + "-shm", _dbPath + "-wal" })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
