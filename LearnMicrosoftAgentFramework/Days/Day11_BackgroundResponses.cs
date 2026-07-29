using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// The background response API (ContinuationToken, AllowBackgroundResponses) is still
// marked experimental (diagnostic MEAI001). We knowingly opt in for this lesson the
// shape may change in a future package release.
#pragma warning disable MEAI001

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 11 - Background responses (long running work + continuation tokens).
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/agents/background-responses?pivots=programming-language-csharp
///
/// The big idea:
///   Some agent tasks take a LONG time deep reasoning, big documents, slow tools.
///   Holding a single HTTP call open for minutes is fragile timeouts, dropped
///   connections, and a client that's stuck waiting. BACKGROUND RESPONSES fix this
///   with a CONTINUATION TOKEN:
///
///     - You enable them with AgentRunOptions.AllowBackgroundResponses = true.
///     - The agent either finishes immediately (no token) OR starts working in the
///       background and hands you a continuation token instead of the final answer.
///     - You then POLL (non streaming) or RESUME THE STREAM (streaming) using that
///       token until it comes back null meaning the work is complete.
///
///   Think of it like a coat check ticket, you get the ticket now and collect the
///   finished coat later, instead of standing at the counter the whole time.
///
///   This lesson shows both consumption patterns:
///     Part 1 - Non-streaming: poll with the continuation token until it's null.
///     Part 2 - Streaming: resume an interrupted stream from where it left off.
///
/// IMPORTANT: Per the docs, background responses are only actually honored by agents
/// on the OpenAI / Azure OpenAI *Responses* API. Other backends (like the Groq
/// Chat Completions endpoint we use here) simply COMPLETE IMMEDIATELY and return no
/// continuation token which is the documented "immediate completion" path. So the
/// SAME polling code below is correct everywhere, it just does one iteration here.
/// </summary>
public sealed class Day11_BackgroundResponses : ILesson
{
    public string Title => "Day 11 - Background responses (continuation tokens)";

    public async Task RunAsync()
    {
        AIAgent agent = AgentFactory.CreateAgent(
            name: "Researcher",
            instructions:
                "You are a meticulous research assistant. Think carefully and give a thorough, "
              + "well structured answer.",
            model: AgentFactory.ToolCapableModel);

        // Part 1 - Non streaming fire the request, then poll with the token.
        // AllowBackgroundResponses = true opts in. If the backend supports it and the
        // task is slow, the FIRST response carries a ContinuationToken instead of the
        // final answer. We then re run with that token (carrying no new user message)
        // to poll, backing off between attempts, until ContinuationToken is null.
        Console.WriteLine("Part 1: Non streaming background response (poll to completion)");
        Console.WriteLine("--------------------------------------------------------------");

        ChatClientAgentRunOptions options = new()
        {
            AllowBackgroundResponses = true,
        };

        // Background responses are resumed against a session, so create one and reuse
        // it across the initial call and every poll.
        AgentSession session = await agent.CreateSessionAsync();

        Console.WriteLine("Submitting a long running research request...");
        AgentResponse response = await agent.RunAsync(
            "Write a detailed comparison of optimistic vs pessimistic concurrency control, "
          + "including trade offs and when to use each.",
            session,
            options: options);

        int poll = 0;
        // The polling loop. When the backend supports background work, this may run
        // many times on an immediate completion backend it simply never enters.
        while (response.ContinuationToken is not null)
        {
            poll++;
            TimeSpan delay = TimeSpan.FromSeconds(Math.Min(2 * poll, 10)); // simple backoff
            Console.WriteLine($"   [poll #{poll}] still running waiting {delay.TotalSeconds:F0}s "
                            + "before checking again...");
            await Task.Delay(delay);

            // Carry the token forward pass no new user input we're just checking.
            options.ContinuationToken = response.ContinuationToken;
            response = await agent.RunAsync([], session, options: options);
        }

        Console.WriteLine(poll == 0
            ? "   (Completed immediately backend returned no continuation token.)"
            : $"   (Completed after {poll} poll(s).)");
        Console.WriteLine();
        Console.WriteLine("Final result:");
        Console.WriteLine(Truncate(response.ToString(), 600));

        Pause();

        // Part 2 - Streaming: resume an interrupted stream with the token.
        // With streaming + background responses, every update EXCEPT the last carries
        // a continuation token. If the stream drops (network blip, client restart),
        // you don't start over  you resume from the last token you saw. We simulate
        // an interruption by breaking out early, then resume with the saved token.
        Console.WriteLine("Part 2: Streaming background response (resume after interruption)");
        Console.WriteLine("----------------------------------------------------------------");

        ChatClientAgentRunOptions streamOptions = new()
        {
            AllowBackgroundResponses = true,
        };

        AgentSession streamSession = await agent.CreateSessionAsync();

        Console.WriteLine("Streaming a long answer, then simulating a dropped connection...");
        Console.Write("Agent: ");

        ResponseContinuationToken? resumeToken = null;
        int updateCount = 0;

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(
            "Explain how the Raft consensus algorithm elects a leader, step by step.",
            streamSession,
            options: streamOptions))
        {
            Console.Write(update);
            resumeToken = update.ContinuationToken;            
            updateCount++;

            // Simulate an interruption partway through IF the backend is streaming a
            // resumable background response (i.e. it handed us a token to resume with).
            if (resumeToken is not null && updateCount >= 3)
            {
                Console.WriteLine();
                Console.WriteLine("   [interrupted] connection dropped saving continuation token.");
                break;
            }
        }

        if (resumeToken is not null)
        {
            // Resume exactly where we left off using the saved token.
            Console.WriteLine("   [resuming] reconnecting from the saved token...");
            Console.Write("Agent (resumed): ");

            streamOptions.ContinuationToken = resumeToken;
            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([], streamSession, options: streamOptions))
            {
                Console.Write(update);
            }
            Console.WriteLine();
        }
        else
        {
            // Immediate completion backend the whole answer streamed in one shot and
            // there was never a token to resume from  which is expected on Groq.
            Console.WriteLine();
            Console.WriteLine("   (Backend streamed the full answer with no resumable token "
                            + "nothing to resume.)");
        }

        Console.WriteLine();
        Console.WriteLine("Takeaway: background responses turn a fragile, long held call into a durable");
        Console.WriteLine("ticket and poll (or resume) flow. Enable with AllowBackgroundResponses, then");
        Console.WriteLine("follow the continuation token until it's null. Same code works whether the");
        Console.WriteLine("backend runs in the background or completes immediately.");
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + " ...";

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press Enter for the next part...");
        Console.ReadLine();
        Console.WriteLine();
    }
}
