using LiveSync.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LiveSync.Api.Infrastructure;

public static class PostgresDatabaseInitializer
{
    public static async Task EnsureDatabaseAndMigrateAsync(
        IServiceProvider services,
        string connectionString,
        IConfiguration configuration,
        ILogger logger)
    {
        if (configuration.GetValue<bool>("Database:EnsureCreatedOnStartup"))
        {
            var maintenanceDatabase = configuration["Database:MaintenanceDatabase"] ?? "postgres";
            await EnsureDatabaseExistsAsync(connectionString, maintenanceDatabase, logger);
        }

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        logger.LogInformation("Applying database migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }

    private static async Task EnsureDatabaseExistsAsync(
        string connectionString,
        string maintenanceDatabase,
        ILogger logger)
    {
        var targetBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDatabase = targetBuilder.Database;

        if (string.IsNullOrWhiteSpace(targetDatabase))
            throw new InvalidOperationException("The PostgreSQL connection string must include a Database value.");

        if (string.Equals(targetDatabase, maintenanceDatabase, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "Target database is the maintenance database '{Database}', skipping database creation check.",
                targetDatabase);
            return;
        }

        var maintenanceBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = maintenanceDatabase,
            Pooling = false
        };

        await using var connection = new NpgsqlConnection(maintenanceBuilder.ConnectionString);
        await connection.OpenAsync();

        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @databaseName";
            existsCommand.Parameters.AddWithValue("databaseName", targetDatabase);

            if (await existsCommand.ExecuteScalarAsync() is not null)
            {
                logger.LogInformation("PostgreSQL database '{Database}' already exists.", targetDatabase);
                return;
            }
        }

        logger.LogInformation("Creating PostgreSQL database '{Database}'...", targetDatabase);

        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"""CREATE DATABASE {QuoteIdentifier(targetDatabase)}""";
        await createCommand.ExecuteNonQueryAsync();

        logger.LogInformation("PostgreSQL database '{Database}' created successfully.", targetDatabase);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }
}
