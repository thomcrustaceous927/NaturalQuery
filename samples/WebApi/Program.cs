using Microsoft.Data.Sqlite;
using NaturalQuery;
using NaturalQuery.Extensions;
using NaturalQuery.Models;
using NaturalQuery.Playground;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("Set OpenAI:ApiKey in config or OPENAI_API_KEY env var.");

// Set up a sample SQLite database
const string dbPath = "sample.db";
SetupSampleDatabase(dbPath);

builder.Services.AddNaturalQuery(options =>
{
    options.Tables = new List<TableSchema>
    {
        new("products", new[]
        {
            new ColumnDef("id", "int"),
            new ColumnDef("name", "string", "product name"),
            new ColumnDef("category", "string"),
            new ColumnDef("price", "double", "USD"),
            new ColumnDef("in_stock", "int", "1=yes, 0=no"),
        })
    };
    options.MaxRetries = 1;
    options.CacheTtlMinutes = 5;
})
.UseOpenAiProvider(apiKey, model: "gpt-4o-mini")
.UseSqliteExecutor($"DataSource={dbPath}")
.UseInMemoryCache()
.UseInMemoryRateLimiter();

var app = builder.Build();

// Map NaturalQuery endpoints (one liner!)
app.MapNaturalQuery("/ask");

// Map the playground (dev only)
if (app.Environment.IsDevelopment())
{
    app.MapNaturalQueryPlayground("/playground", apiPath: "/ask");
}

app.MapGet("/", () => Results.Redirect("/playground"));

app.Run();

static void SetupSampleDatabase(string path)
{
    if (File.Exists(path)) File.Delete(path);
    using var conn = new SqliteConnection($"DataSource={path}");
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        CREATE TABLE products (
            id INTEGER PRIMARY KEY,
            name TEXT, category TEXT, price REAL, in_stock INTEGER DEFAULT 1
        );
        INSERT INTO products VALUES (1, 'Gold Ring', 'Jewelry', 299.99, 1);
        INSERT INTO products VALUES (2, 'Silver Necklace', 'Jewelry', 149.99, 1);
        INSERT INTO products VALUES (3, 'Diamond Earrings', 'Jewelry', 499.99, 0);
        INSERT INTO products VALUES (4, 'Leather Watch', 'Accessories', 199.99, 1);
        INSERT INTO products VALUES (5, 'Titanium Bracelet', 'Accessories', 89.99, 1);
        """;
    cmd.ExecuteNonQuery();
}
