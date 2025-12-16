using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relex.Tests;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await InitializeDatabaseAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext configuration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<Relex.Api.Infrastructure.RelexDbContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Remove existing db connection string configuration
            var dbConnectionDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbConnection));

            if (dbConnectionDescriptor != null)
            {
                services.Remove(dbConnectionDescriptor);
            }

            // Add DbContext pointing to the Testcontainer
            services.AddDbContext<Relex.Api.Infrastructure.RelexDbContext>(options =>
            {
                options.UseNpgsql(_dbContainer.GetConnectionString());
            });
            
            // Override configuration to ensure other components use the correct string
            var configDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConfiguration));
        });

        // Override config for raw connection strings used in BulkUpsert
        builder.UseSetting("ConnectionStrings:DefaultConnection", _dbContainer.GetConnectionString());
    }

    private async Task InitializeDatabaseAsync()
    {
        using var connection = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await connection.OpenAsync();

        // 1. Create Schema
        var initScript = @"
            CREATE TABLE locations (
                id SMALLINT PRIMARY KEY,
                code TEXT NOT NULL UNIQUE
            );

            CREATE TABLE products (
                id INT PRIMARY KEY,
                code TEXT NOT NULL UNIQUE
            );

            CREATE TABLE orders (
                id UUID NOT NULL,
                location_id SMALLINT NOT NULL REFERENCES locations(id),
                product_id INT NOT NULL REFERENCES products(id),
                order_date DATE NOT NULL,
                quantity INT NOT NULL CHECK (quantity > 0),
                submitted_by TEXT NOT NULL,
                submitted_at TIMESTAMPTZ NOT NULL,
                status INT NOT NULL DEFAULT 0,
                PRIMARY KEY (order_date, id)
            ) PARTITION BY RANGE (order_date);

            CREATE TABLE orders_default PARTITION OF orders DEFAULT;
        ";

        using (var command = new NpgsqlCommand(initScript, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // 2. Create Partitions (Simplified for tests)
        var partitionScript = @"
            CREATE TABLE orders_2023 PARTITION OF orders 
            FOR VALUES FROM ('2023-01-01') TO ('2024-01-01');
        ";
        
        using (var command = new NpgsqlCommand(partitionScript, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // 3. Seed Lookups
        var seedScript = @"
            INSERT INTO locations (id, code) VALUES (1, 'LOC-0001'), (2, 'LOC-0002');
            INSERT INTO products (id, code) VALUES (101, 'PROD-00001'), (102, 'PROD-00002');
        ";

        using (var command = new NpgsqlCommand(seedScript, connection))
        {
            await command.ExecuteNonQueryAsync();
        }
    }
}
