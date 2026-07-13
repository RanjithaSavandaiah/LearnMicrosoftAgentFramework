using LearnMicrosoftAgentFramework;
using LearnMicrosoftAgentFramework.Days;

// Every "day" is a lesson. Add new days here and they appear in the menu.
List<ILesson> lessons =
[
    new Day1_Intro(),
    new Day2_FirstAgent(),
    new Day3_MultiTurn(),
    new Day4_MemoryPersistence(),
    new Day5_Workflows(),
    new Day6_Hosting(),
    new Day7_RunningAgents(),
];

// If a day number is passed on the command line (e.g. "dotnet run 2"), run it
// directly. Otherwise show an interactive menu.
if (args.Length > 0 && int.TryParse(args[0], out int arg) && arg >= 1 && arg <= lessons.Count)
{
    await RunLessonAsync(lessons[arg - 1]);
    return;
}

while (true)
{
    Console.WriteLine();
    Console.WriteLine("=== Microsoft Agent Framework - learning journey ===");
    for (int i = 0; i < lessons.Count; i++)
    {
        Console.WriteLine($"  {i + 1}. {lessons[i].Title}");
    }
    Console.WriteLine("  0. Exit");
    Console.Write("Pick a day: ");

    string? input = Console.ReadLine();
    if (!int.TryParse(input, out int choice) || choice < 0 || choice > lessons.Count)
    {
        Console.WriteLine("Please enter a valid number.");
        continue;
    }

    if (choice == 0)
    {
        break;
    }

    await RunLessonAsync(lessons[choice - 1]);
}

static async Task RunLessonAsync(ILesson lesson)
{
    Console.WriteLine();
    Console.WriteLine($"--- {lesson.Title} ---");
    try
    {
        await lesson.RunAsync();
    }
    catch (InvalidOperationException ex)
    {
        // Most commonly a missing GROQ_API_KEY environment variable.
        Console.WriteLine($"Could not run the lesson: {ex.Message}");
    }
    Console.WriteLine();
}