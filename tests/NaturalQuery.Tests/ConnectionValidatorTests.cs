using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NaturalQuery.Validation;

namespace NaturalQuery.Tests;

public class ConnectionValidatorTests
{
    // ==================== IsSqlServerReadOnly ====================

    [Fact]
    public void IsSqlServerReadOnly_Should_Return_True_When_ApplicationIntent_ReadOnly()
    {
        var connStr = "Server=myserver;Database=mydb;ApplicationIntent=ReadOnly;";
        ConnectionValidator.IsSqlServerReadOnly(connStr).Should().BeTrue();
    }

    [Fact]
    public void IsSqlServerReadOnly_Should_Return_True_When_ReadOnly_True()
    {
        var connStr = "Server=myserver;Database=mydb;ReadOnly=true;";
        ConnectionValidator.IsSqlServerReadOnly(connStr).Should().BeTrue();
    }

    [Fact]
    public void IsSqlServerReadOnly_Should_Return_True_Case_Insensitive()
    {
        var connStr = "Server=myserver;Database=mydb;applicationintent=readonly;";
        ConnectionValidator.IsSqlServerReadOnly(connStr).Should().BeTrue();
    }

    [Fact]
    public void IsSqlServerReadOnly_Should_Return_False_Without_ReadOnly_Flag()
    {
        var connStr = "Server=myserver;Database=mydb;Trusted_Connection=true;";
        ConnectionValidator.IsSqlServerReadOnly(connStr).Should().BeFalse();
    }

    // ==================== IsPostgresReadOnly ====================

    [Fact]
    public void IsPostgresReadOnly_Should_Return_True_With_TargetSessionAttrs()
    {
        var connStr = "Host=myhost;Database=mydb;Target Session Attrs=read-only;";
        ConnectionValidator.IsPostgresReadOnly(connStr).Should().BeTrue();
    }

    [Fact]
    public void IsPostgresReadOnly_Should_Return_True_With_DefaultTransactionReadOnly()
    {
        var connStr = "Host=myhost;Database=mydb;Options=-c default_transaction_read_only=on;";
        ConnectionValidator.IsPostgresReadOnly(connStr).Should().BeTrue();
    }

    [Fact]
    public void IsPostgresReadOnly_Should_Return_False_Without_ReadOnly_Config()
    {
        var connStr = "Host=myhost;Database=mydb;Username=user;Password=pass;";
        ConnectionValidator.IsPostgresReadOnly(connStr).Should().BeFalse();
    }

    // ==================== WarnIfNotReadOnly - SQLite ====================

    [Fact]
    public void WarnIfNotReadOnly_Should_Not_Warn_For_Sqlite()
    {
        var loggerMock = new Mock<ILogger>();
        ConnectionValidator.WarnIfNotReadOnly("Data Source=mydb.db;", "sqlite", loggerMock.Object);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    // ==================== WarnIfNotReadOnly - Athena ====================

    [Fact]
    public void WarnIfNotReadOnly_Should_Not_Warn_For_Athena()
    {
        var loggerMock = new Mock<ILogger>();
        ConnectionValidator.WarnIfNotReadOnly("some-athena-connection", "athena", loggerMock.Object);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    // ==================== WarnIfNotReadOnly - SQL Server ====================

    [Fact]
    public void WarnIfNotReadOnly_Should_Warn_For_NonReadOnly_SqlServer()
    {
        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true);

        ConnectionValidator.WarnIfNotReadOnly(
            "Server=myserver;Database=mydb;Trusted_Connection=true;",
            "sqlserver",
            loggerMock.Object);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void WarnIfNotReadOnly_Should_Not_Warn_For_ReadOnly_SqlServer()
    {
        var loggerMock = new Mock<ILogger>();

        ConnectionValidator.WarnIfNotReadOnly(
            "Server=myserver;Database=mydb;ApplicationIntent=ReadOnly;",
            "sqlserver",
            loggerMock.Object);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    // ==================== WarnIfNotReadOnly - Unknown provider ====================

    [Fact]
    public void WarnIfNotReadOnly_Should_Not_Warn_For_Unknown_Provider()
    {
        var loggerMock = new Mock<ILogger>();
        ConnectionValidator.WarnIfNotReadOnly("some-connection", "cosmosdb", loggerMock.Object);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
