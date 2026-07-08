using Microsoft.Agents.AI.Workflows;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 5 - Workflows (chaining steps together).
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/get-started/workflows?pivots=programming-language-csharp
///
/// Everything so far has been ONE agent answering. A workflow lets you chain
/// several steps - called EXECUTORS - where each step processes data and passes
/// its result to the next, like a pipeline.
///
/// The classic sample pipeline is:
///   "hello world"  ->  [Uppercase]  ->  "HELLO WORLD"  ->  [Reverse]  ->  "DLROW OLLEH"
///
/// This lesson shows the two ways to write an executor:
///   1. Uppercase - a simple Func turned into an executor with BindAsExecutor.
///   2. Reverse   - a class deriving from Executor TInput, TOutput (handy when a
///                  step needs its own state or more logic).
///
/// NOTE: Workflows live in a SEPARATE package, Microsoft.Agents.AI.Workflows,
/// which we added for this day. This sample is pure string processing, so unlike
/// the other days - it needs no API key and runs offline.
/// </summary>
public sealed class Day5_Workflows : ILesson
{
    public string Title => "Day 5 - Workflows (chaining executors)";

    public async Task RunAsync()
    {
        Console.WriteLine("Pipeline: text -> [Uppercase] -> [Reverse] -> output");
        Console.WriteLine("----------------------------------------------------");

        // Step 1 - a Func based executor. BindAsExecutor wraps the delegate as a
        // workflow step and gives it a name we can see in the output events.
        ExecutorBinding uppercase = new Func<string, string>(
                text => text.ToUpperInvariant())
            .BindAsExecutor("Uppercase");

        // Step 2 - a class-based executor (defined at the bottom of this file). Use
        // this style when a step needs its own fields or more involved logic.
        ReverseTextExecutor reverse = new();

        // Build the workflow: start at Uppercase, then flow to Reverse, and mark
        // Reverse's output as the workflow's output.
        WorkflowBuilder builder = new(uppercase);
        builder.AddEdge(uppercase, reverse);
        builder.WithOutputFrom(reverse);
        Workflow workflow = builder.Build();

        // Run it. The workflow processes the input through every step in order.
        const string input = "Hello, World!";
        Console.WriteLine($"Input: {input}");

        Run run = await InProcessExecution.RunAsync(workflow, input);

        // Each executor raises an ExecutorCompletedEvent when it finishes. We print
        // them so you can watch the data change as it flows through the pipeline.
        foreach (WorkflowEvent evt in run.NewEvents)
        {
            if (evt is ExecutorCompletedEvent completed)
            {
                Console.WriteLine($"   [{completed.ExecutorId}] -> {completed.Data}");
            }
        }
    }

    /// <summary>
    /// A class based executor: it takes a string and returns it reversed. Deriving
    /// from <see cref="Executor{TInput, TOutput}"/> is the alternative to the Func
    /// style - useful when a step grows beyond a one liner. The <c>base("Reverse")</c>
    /// call names the step so it shows up in the output events.
    /// </summary>
    private sealed class ReverseTextExecutor() : Executor<string, string>("Reverse")
    {
        public override ValueTask<string> HandleAsync(
            string message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            char[] characters = message.ToCharArray();
            Array.Reverse(characters);
            return ValueTask.FromResult(new string(characters));
        }
    }
}
