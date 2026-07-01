namespace LearnMicrosoftAgentFramework;

/// <summary>
/// Every day's sample implements this so <c>Program.cs</c> can list and run them
/// with a simple menu. As the learning journey grows, just add a new class.
/// </summary>
public interface ILesson
{
    /// <summary>Short title shown in the menu, e.g. "Day 2 - Your first agent".</summary>
    string Title { get; }

    /// <summary>Runs the lesson.</summary>
    Task RunAsync();
}
