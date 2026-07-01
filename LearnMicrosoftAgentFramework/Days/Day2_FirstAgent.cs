using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 2 - Your first agent (done right) + adding tools.
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/get-started/your-first-agent?pivots=programming-language-csharp
///   https://learn.microsoft.com/en-us/agent-framework/get-started/add-tools?pivots=programming-language-csharp
///
/// What improved since Day 1:
///   - The API key now comes from an environment variable (see AgentFactory),
///     instead of being hard-coded.
///   - We stream the reply with RunStreamingAsync so text appears as it is generated.
///   - We give the agent a tool it can call to answer questions.
/// </summary>
public sealed class Day2_FirstAgent : ILesson
{
    public string Title => "Day 2 - Env key + streaming + tools";

    public async Task RunAsync()
    {
        // Part 1 - Streaming.
        // Same idea as Day 1, but the key is read from GROQ_API_KEY (via AgentFactory)
        // and we stream the response instead of waiting for the whole thing.
        Console.WriteLine("Part 1: A streaming agent");
        Console.WriteLine("-------------------------");

        AIAgent agent = AgentFactory.CreateAgent(
            instructions: "You are a friendly assistant. Keep your answers brief.");

        await foreach (AgentResponseUpdate update in
            agent.RunStreamingAsync("What is the largest city in France?"))
        {
            Console.Write(update);
        }
        Console.WriteLine();

        // Part 2 - Adding tools.
        // A tool is just a C# method exposed to the model. When the question needs
        // it, the model calls the method, gets the result, and uses it in the answer.
        // NOTE: we use RunAsync (not streaming) here, and a model that is reliable at
        // tool calling.
        Console.WriteLine();
        Console.WriteLine("Part 2: An agent with a tool");
        Console.WriteLine("----------------------------");

        AIAgent weatherAgent = AgentFactory.CreateAgent(
            instructions: "You are a helpful assistant. Use the GetWeather tool to answer "
                        + "weather questions, and base your answer on its result. Keep answers brief.",
            tools: [AIFunctionFactory.Create(GetWeather)],
            model: AgentFactory.ToolCapableModel);

        // The model decides on its own to call GetWeather to answer this.
        // The printed answer won't match the tool's return string word for word: the
        // tool result is fed back to the model, which then writes its own reply using
        // that data. So the facts come from the tool, but the wording is the model's.
        Console.WriteLine(await weatherAgent.RunAsync("What is the weather like in Amsterdam?"));
    }

    /// <summary>
    /// A tool the agent can call. The <see cref="DescriptionAttribute"/> text helps
    /// the model understand what the tool (and each parameter) does.
    /// </summary>
    [Description("Gets the current weather for a given location.")]
    private static string GetWeather(
        [Description("The city or location to get the weather for.")] string location)
    {
        // Printed so you can SEE the tool actually run. If the model hallucinates
        // instead of calling the tool, this line will not appear.
        Console.WriteLine($"   [tool GetWeather called for '{location}']");

        // In a real app this would call a weather service. We return a fixed
        // value so the sample is deterministic and needs no extra setup.
        return $"The weather in {location} is cloudy with a high of 15\u00B0C.";
    }
}
