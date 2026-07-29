using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 12 - Agent as a tool (multi agent composition).
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/agents/agent-as-tool?pivots=programming-language-csharp
///
/// The big idea:
///   Everything so far gave an agent PLAIN functions as tools. But an agent is
///   itself just something that takes text and returns text which is exactly the
///   shape of a tool. AsAIFunction() wraps a whole AIAgent so ANOTHER agent can
///   call it like any other function. That lets you build a COORDINATOR agent that
///   delegates to focused SPECIALIST agents, each with its own persona, tools and
///   model the foundation of multi agent systems.
///
///   This lesson builds a small "travel desk":
///     - A weather specialist agent (its own tool).
///     - A currency specialist agent (its own tool).
///     - A coordinator agent that is given the two specialists AS TOOLS and routes
///       each question to the right one, then composes a final answer.
/// </summary>
public sealed class Day12_AgentAsTool : ILesson
{
    public string Title => "Day 12 - Agent as a tool (multi-agent)";

    public async Task RunAsync()
    {
        // Part 1 - Build two focused specialist agents.
        // Each is an ordinary agent with a narrow job and its own tool. On their own
        // they answer only their speciality.
        Console.WriteLine("Part 1: Two specialist agents");
        Console.WriteLine("-----------------------------");

        AIAgent weatherAgent = AgentFactory.CreateAgent(
            name: "WeatherExpert",
            instructions: "You are a weather expert. Use the tool to report the forecast. Be concise.",
            tools: [AIFunctionFactory.Create(GetForecast)],
            model: AgentFactory.ToolCapableModel);

        AIAgent currencyAgent = AgentFactory.CreateAgent(
            name: "CurrencyExpert",
            instructions: "You are a currency expert. Use the tool to convert money. Be concise.",
            tools: [AIFunctionFactory.Create(ConvertCurrency)],
            model: AgentFactory.ToolCapableModel);

        Console.WriteLine("WeatherExpert: " + await weatherAgent.RunAsync("What's the weather in Tokyo?"));
        Console.WriteLine("CurrencyExpert: " + await currencyAgent.RunAsync("Convert 100 USD to JPY."));

        Pause();

        // Part 2 - Wrap each specialist AS A TOOL and hand them to a coordinator.
        // AsAIFunction() turns an agent into an AIFunction. We give it a clear name +
        // description so the coordinator's model knows WHEN to call it exactly like
        // documenting any tool.
        Console.WriteLine("Part 2: A coordinator that delegates to specialists");
        Console.WriteLine("---------------------------------------------------");

        AITool weatherTool = weatherAgent.AsAIFunction(new AIFunctionFactoryOptions
        {
            Name = "AskWeatherExpert",
            Description = "Ask the weather specialist about the forecast for a city.",
        });

        AITool currencyTool = currencyAgent.AsAIFunction(new AIFunctionFactoryOptions
        {
            Name = "AskCurrencyExpert",
            Description = "Ask the currency specialist to convert an amount between currencies.",
        });

        AIAgent coordinator = AgentFactory.CreateAgent(
            name: "TravelDesk",
            instructions:
                "You are a travel desk coordinator. You do not answer weather or currency "
              + "questions yourself delegate them to the matching expert tool, then combine "
              + "the results into one helpful reply.",
            tools: [weatherTool, currencyTool],
            model: AgentFactory.ToolCapableModel);

        // One prompt that needs BOTH specialists the coordinator calls each tool
        // (which runs the underlying agent) and stitches the answers together.
        Console.WriteLine("Traveller: I'm flying to Tokyo tomorrow with 200 USD spending money");
        Console.WriteLine("           what should I expect?");
        Console.WriteLine();
        Console.WriteLine("TravelDesk: " + await coordinator.RunAsync(
            "I'm flying to Tokyo tomorrow with 200 USD spending money. What weather should I "
          + "expect and how much is that in local currency?"));

        Console.WriteLine();
        Console.WriteLine("Takeaway: AsAIFunction() turns any agent into a callable tool, so a");
        Console.WriteLine("coordinator can delegate to specialist agents. Compose small, focused");
        Console.WriteLine("agents into larger systems instead of building one giant do everything agent.");
    }

    [Description("Gets tomorrow's weather forecast for a city.")]
    private static string GetForecast(
        [Description("The city name, e.g. Tokyo.")] string city)
    {
        Console.WriteLine($"   [tool GetForecast called for '{city}']");
        return $"{city}: sunny with light cloud, high of 22C, low of 14C.";
    }

    [Description("Converts an amount of money from one currency to another.")]
    private static string ConvertCurrency(
        [Description("The amount to convert.")] decimal amount,
        [Description("The 3 letter source currency code, e.g. USD.")] string from,
        [Description("The 3 letter target currency code, e.g. JPY.")] string to)
    {
        Console.WriteLine($"   [tool ConvertCurrency called: {amount} {from}->{to}]");

        // Deterministic fake rates so the sample needs no backend.
        decimal rate = (from.ToUpperInvariant(), to.ToUpperInvariant()) switch
        {
            ("USD", "JPY") => 150m,
            ("USD", "EUR") => 0.92m,
            ("EUR", "USD") => 1.09m,
            _ => 1m,
        };

        return $"{amount} {from.ToUpperInvariant()} = {amount * rate:0.00} {to.ToUpperInvariant()}.";
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press Enter for the next part...");
        Console.ReadLine();
        Console.WriteLine();
    }
}
