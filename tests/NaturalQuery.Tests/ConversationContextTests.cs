using FluentAssertions;
using NaturalQuery.Models;

namespace NaturalQuery.Tests;

public class ConversationContextTests
{
    [Fact]
    public void AddTurn_Should_Add_To_Turns_List()
    {
        var context = new ConversationContext();

        context.AddTurn("question 1", "SELECT 1");

        context.Turns.Should().HaveCount(1);
        context.Turns[0].Question.Should().Be("question 1");
        context.Turns[0].Sql.Should().Be("SELECT 1");
    }

    [Fact]
    public void AddTurn_Should_Preserve_Order()
    {
        var context = new ConversationContext();

        context.AddTurn("first", "SELECT 1");
        context.AddTurn("second", "SELECT 2");
        context.AddTurn("third", "SELECT 3");

        context.Turns.Should().HaveCount(3);
        context.Turns[0].Question.Should().Be("first");
        context.Turns[1].Question.Should().Be("second");
        context.Turns[2].Question.Should().Be("third");
    }

    [Fact]
    public void MaxTurns_Should_Default_To_5()
    {
        var context = new ConversationContext();

        context.MaxTurns.Should().Be(5);
    }

    [Fact]
    public void AddTurn_Should_Remove_Oldest_When_Exceeding_MaxTurns()
    {
        var context = new ConversationContext { MaxTurns = 3 };

        context.AddTurn("q1", "sql1");
        context.AddTurn("q2", "sql2");
        context.AddTurn("q3", "sql3");
        context.AddTurn("q4", "sql4");

        context.Turns.Should().HaveCount(3);
        context.Turns[0].Question.Should().Be("q2");
        context.Turns[1].Question.Should().Be("q3");
        context.Turns[2].Question.Should().Be("q4");
    }

    [Fact]
    public void AddTurn_With_Default_MaxTurns_Should_Keep_5()
    {
        var context = new ConversationContext();

        for (int i = 1; i <= 7; i++)
            context.AddTurn($"q{i}", $"sql{i}");

        context.Turns.Should().HaveCount(5);
        context.Turns[0].Question.Should().Be("q3");
        context.Turns[4].Question.Should().Be("q7");
    }

    [Fact]
    public void New_Context_Should_Have_Empty_Turns()
    {
        var context = new ConversationContext();

        context.Turns.Should().BeEmpty();
    }

    [Fact]
    public void MaxTurns_Can_Be_Customized()
    {
        var context = new ConversationContext { MaxTurns = 10 };

        context.MaxTurns.Should().Be(10);
    }
}
