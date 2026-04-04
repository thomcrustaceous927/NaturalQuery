namespace NaturalQuery.Models;

/// <summary>
/// Maintains conversation history for follow-up questions.
/// Allows the LLM to understand references like "now filter by cancelled ones".
/// </summary>
public class ConversationContext
{
    /// <summary>Previous question-answer pairs in chronological order.</summary>
    public List<ConversationTurn> Turns { get; set; } = new();

    /// <summary>Maximum number of turns to send to the LLM. Default: 5.</summary>
    public int MaxTurns { get; set; } = 5;

    /// <summary>Adds a turn to the conversation history.</summary>
    public void AddTurn(string question, string sql)
    {
        Turns.Add(new ConversationTurn(question, sql));
        if (Turns.Count > MaxTurns)
            Turns.RemoveAt(0);
    }
}

/// <summary>A single question-answer pair in a conversation.</summary>
public record ConversationTurn(string Question, string Sql);
