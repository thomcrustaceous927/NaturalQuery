using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NaturalQuery;
using NaturalQuery.Extensions;
using NaturalQuery.Models;

// This sample demonstrates NaturalQuery with SQLite and OpenAI.
// Set the OPENAI_API_KEY environment variable before running.

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Set the OPENAI_API_KEY environment variable and try again.");
    return;
}

// Create a sample SQLite database
const string dbPath = "sample.db";
if (File.Exists(dbPath)) File.Delete(dbPath);

await using var setupConn = new SqliteConnection($"DataSource={dbPath}");
await setupConn.OpenAsync();

using var cmd = setupConn.CreateCommand();
cmd.CommandText = """
    CREATE TABLE products (
        id INTEGER PRIMARY KEY,
        name TEXT NOT NULL,
        category TEXT NOT NULL,
        price REAL NOT NULL,
        in_stock INTEGER NOT NULL DEFAULT 1
    );
    INSERT INTO products VALUES (1, 'Gold Ring', 'Jewelry', 299.99, 1);
    INSERT INTO products VALUES (2, 'Silver Necklace', 'Jewelry', 149.99, 1);
    INSERT INTO products VALUES (3, 'Diamond Earrings', 'Jewelry', 499.99, 0);
    INSERT INTO products VALUES (4, 'Leather Watch', 'Accessories', 199.99, 1);
    INSERT INTO products VALUES (5, 'Titanium Bracelet', 'Accessories', 89.99, 1);
    INSERT INTO products VALUES (6, 'Pearl Pendant', 'Jewelry', 179.99, 1);
    INSERT INTO products VALUES (7, 'Steel Cufflinks', 'Accessories', 59.99, 0);
    INSERT INTO products VALUES (8, 'Rose Gold Band', 'Jewelry', 349.99, 1);
    """;
cmd.ExecuteNonQuery();
setupConn.Close();

// Configure NaturalQuery
var services = new ServiceCollection();

services.AddLogging();
services.AddNaturalQuery(options =>
{
    options.Tables = new List<TableSchema>
    {
        new("products", new[]
        {
            new ColumnDef("id", "int", "primary key"),
            new ColumnDef("name", "string", "product name"),
            new ColumnDef("category", "string", "Jewelry or Accessories"),
            new ColumnDef("price", "double", "price in USD"),
            new ColumnDef("in_stock", "int", "1 if available, 0 if not"),
        })
    };
})
.UseOpenAiProvider(apiKey, model: "gpt-4o-mini")
.UseSqliteExecutor($"DataSource={dbPath}");

var provider = services.BuildServiceProvider();
var engine = provider.GetRequiredService<INaturalQueryEngine>();

// Ask questions
var questions = new[]
{
    "How many products do we have?",
    "Products by category",
    "What is the most expensive product?",
    "List all products that are in stock with their prices",
};

foreach (var question in questions)
{
    Console.WriteLine($"\n{new string('=', 60)}");
    Console.WriteLine($"Question: {question}");
    Console.WriteLine(new string('=', 60));

    try
    {
        var result = await engine.AskAsync(question);

        Console.WriteLine($"SQL: {result.Sql}");
        Console.WriteLine($"Chart: {result.ChartType}");
        Console.WriteLine($"Title: {result.Title}");

        if (result.ChartData != null)
        {
            Console.WriteLine("\nData:");
            foreach (var point in result.ChartData)
                Console.WriteLine($"  {point.Label}: {point.Value}");
        }

        if (result.TableData != null)
        {
            Console.WriteLine($"\n{result.TableData.Count} rows:");
            foreach (var row in result.TableData)
            {
                var values = string.Join(" | ", row.Select(kv => $"{kv.Key}: {kv.Value}"));
                Console.WriteLine($"  {values}");
            }
        }

        Console.WriteLine($"\nTokens: {result.TokensUsed}, Time: {result.ElapsedMs}ms");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

// Cleanup
File.Delete(dbPath);
