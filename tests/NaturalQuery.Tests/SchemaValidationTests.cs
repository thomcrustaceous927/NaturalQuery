using FluentAssertions;
using NaturalQuery.Models;
using NaturalQuery.Validation;

namespace NaturalQuery.Tests;

public class SchemaValidationTests
{
    private static List<TableSchema> CreateTables() => new()
    {
        new("users", new[]
        {
            new ColumnDef("id", "string"),
            new ColumnDef("name", "string"),
            new ColumnDef("status", "string"),
        }),
        new("orders", new[]
        {
            new ColumnDef("id", "string"),
            new ColumnDef("user_id", "string"),
            new ColumnDef("total", "decimal"),
        })
    };

    [Fact]
    public void Valid_Query_With_Known_Tables_Should_Return_Null()
    {
        var result = SchemaValidator.ValidateColumns(
            "SELECT name FROM users WHERE status = 'active'",
            CreateTables());

        result.Should().BeNull();
    }

    [Fact]
    public void Query_With_Unknown_Table_Should_Return_Error()
    {
        var result = SchemaValidator.ValidateColumns(
            "SELECT * FROM unknown_table",
            CreateTables());

        result.Should().NotBeNull();
        result.Should().Contain("Unknown table");
        result.Should().Contain("unknown_table");
    }

    [Fact]
    public void CTE_Aliases_Should_Not_Be_Flagged_As_Unknown()
    {
        var result = SchemaValidator.ValidateColumns(
            "WITH latest AS (SELECT * FROM users) SELECT * FROM latest",
            CreateTables());

        result.Should().BeNull();
    }

    [Fact]
    public void Empty_Tables_List_Should_Flag_Unknown_Table()
    {
        var result = SchemaValidator.ValidateColumns(
            "SELECT * FROM anything",
            new List<TableSchema>());

        // Empty tables means no known tables, so "anything" is unknown
        result.Should().NotBeNull();
        result.Should().Contain("Unknown table");
    }

    [Fact]
    public void Null_Tables_Should_Return_Null()
    {
        var result = SchemaValidator.ValidateColumns(
            "SELECT * FROM anything",
            null!);

        result.Should().BeNull();
    }

    [Fact]
    public void Empty_Sql_Should_Return_Null()
    {
        var result = SchemaValidator.ValidateColumns("", CreateTables());

        result.Should().BeNull();
    }

    [Fact]
    public void Null_Sql_Should_Return_Null()
    {
        var result = SchemaValidator.ValidateColumns(null!, CreateTables());

        result.Should().BeNull();
    }

    [Fact]
    public void Query_With_JOIN_On_Known_Table_Should_Pass()
    {
        var result = SchemaValidator.ValidateColumns(
            "SELECT u.name, o.total FROM users u JOIN orders o ON u.id = o.user_id",
            CreateTables());

        result.Should().BeNull();
    }

    [Fact]
    public void Query_With_Unknown_Table_Should_List_Available_Tables()
    {
        var result = SchemaValidator.ValidateColumns(
            "SELECT * FROM products",
            CreateTables());

        result.Should().Contain("Available");
        result.Should().Contain("users");
        result.Should().Contain("orders");
    }

    [Fact]
    public void Multiple_CTEs_Should_Not_Be_Flagged()
    {
        var result = SchemaValidator.ValidateColumns(
            "WITH a AS (SELECT * FROM users), b AS (SELECT * FROM orders) SELECT * FROM a JOIN b ON a.id = b.user_id",
            CreateTables());

        result.Should().BeNull();
    }
}
