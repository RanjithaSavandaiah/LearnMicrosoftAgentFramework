using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// The harness/loop API (LoopAgent, LoopEvaluator and friends) is still marked
// experimental (diagnostic MAAI001). We knowingly opt in for this lesson, the
// shape may change in a future package release.
#pragma warning disable MAAI001

namespace LearnMicrosoftAgentFramework.Days;

/// <summary>
/// Day 8 - The Agent Harness (a self driving agent loop).
/// Based on:
///   https://learn.microsoft.com/en-us/agent-framework/agents/harness?pivots=programming-language-csharp
///   https://learn.microsoft.com/en-us/agent-framework/get-started/harness?pivots=programming-language-csharp
///
/// The big idea:
///   Everything so far ran an agent EXACTLY ONCE per prompt. But real tasks often
///   aren't "one and done" - a first draft may be incomplete, off brief, or simply
///   not good enough. A HARNESS wraps an ordinary agent in a loop and keeps
///   re invoking it - feeding feedback back in each time - until a QUALITY GATE
///   decides the work is finished (or a safety cap is hit).
///
///   In this framework the harness is a <see cref="LoopAgent"/>, it wraps any
///   AIAgent and drives it with one or more <see cref="LoopEvaluator"/> gates. The
///   beauty is that a LoopAgent IS itself an AIAgent - so you call RunAsync on it
///   exactly like any other agent, and the whole self correcting loop is hidden
///   behind that single call.
///
///   This lesson builds the SAME worker pattern four ways, each with a smarter gate:
///     Part 1 - CompletionMarkerLoopEvaluator: loop until the agent emits a marker.
///     Part 2 - DelegateLoopEvaluator: a deterministic C# rule you write yourself.
///     Part 3 - AIJudgeLoopEvaluator: an LLM as judge grades the work each round.
///     Part 4 - MULTIPLE gates combined: a C# rule AND an AI judge must both pass.
///
///   To make the invisible loop VISIBLE, most parts are wrapped in a tiny logging
///   evaluator (see <see cref="LoggingLoopEvaluator"/> at the bottom) that prints
///   the iteration number and whether that gate accepted or asked for another pass.
///
/// NOTE: Needs GROQ_API_KEY. The tool capable model is used because looping,
/// self correcting agents need reliable instruction following.
/// </summary>
public sealed class Day8_Harness : ILesson
{
    public string Title => "Day 8 - Agent harness (self driving loop)";

    public async Task RunAsync()
    {
        // Part 1 - Completion marker gate.
        // The simplest harness: keep looping until the agent writes an agreed upon
        // "I'm done" marker. Until the marker appears, the evaluator automatically
        // feeds back "keep working, and emit the marker when finished". This turns a
        // vague task into a self terminating one WITHOUT any custom code.
        Console.WriteLine("Part 1: Loop until the agent signals completion (marker gate)");
        Console.WriteLine("------------------------------------------------------------");

        const string DoneMarker = "TASK-COMPLETE";

        AIAgent writer = AgentFactory.CreateAgent(
            name: "Copywriter",
            instructions:
                "You are a meticulous copywriter. Draft, then critically review and improve "
              + "your own work until it is polished. When and only when the result is "
              + $"genuinely finished, end your message with the exact marker {DoneMarker}.",
            model: AgentFactory.ToolCapableModel);

        LoopAgent markerHarness = new(
            writer,
            new LoggingLoopEvaluator("marker", new CompletionMarkerLoopEvaluator(DoneMarker)),
            new LoopAgentOptions
            {
                // Safety cap: never invoke the wrapped agent more than 4 times.
                MaxIterations = 4,

                // NonStreamingReturnsLastResponseOnly: for a plain RunAsync we only
                // want the FINAL accepted answer, not the whole draft/feedback
                // transcript of every iteration. This keeps markerResult clean.
                NonStreamingReturnsLastResponseOnly = true,
            });

        AgentResponse markerResult = await markerHarness.RunAsync(
            "Write a punchy three line product tagline for a reusable stainless steel water bottle.");

        // PROOF that the marker gets stripped: print the RAW response (marker still
        // present) next to the CLEANED response we would actually show a user.
        string rawText = markerResult.ToString();
        string cleanText = rawText.Replace(DoneMarker, string.Empty).Trim();

        Console.WriteLine();
        Console.WriteLine($"Raw response contains marker?    {rawText.Contains(DoneMarker)}");
        Console.WriteLine("--- Raw (exactly as the agent wrote it) ---");
        Console.WriteLine(rawText);
        Console.WriteLine("--- Cleaned (marker stripped, shown to user) ---");
        Console.WriteLine(cleanText);
        Console.WriteLine($"Cleaned response contains marker? {cleanText.Contains(DoneMarker)}");

        Pause();

        // Part 2 - Deterministic C# gate.
        // Sometimes "done" is an objective, checkable fact a length limit, a
        // required keyword, a JSON shape. A DelegateLoopEvaluator lets you express
        // that rule in plain C#. You inspect the LoopContext (what the agent just
        // produced, which iteration we're on, feedback so far) and return either
        // LoopEvaluation.Stop() to accept, or LoopEvaluation.Continue(feedback) to
        // send it back for another pass with concrete, actionable guidance.
        Console.WriteLine();
        Console.WriteLine("Part 2: Enforce a hard constraint with a C# gate (delegate)");
        Console.WriteLine("-----------------------------------------------------------");

        const int MaxWords = 12;

        AIAgent sloganAgent = AgentFactory.CreateAgent(
            name: "Sloganeer",
            instructions: "You write memorable marketing slogans. Return only the slogan text.",
            model: AgentFactory.ToolCapableModel);

        LoopAgent lengthHarness = new(
            sloganAgent,
            new DelegateLoopEvaluator((context, _) =>
            {
                string text = context.LastResponse?.ToString().Trim() ?? string.Empty;
                int wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                bool passes = wordCount > 0 && wordCount <= MaxWords;

                Console.WriteLine(
                    $"   [gate:words] iteration {context.Iteration}: {wordCount} words "
                  + $"-> {(passes ? "ACCEPT" : "RETRY")}: \"{text}\"");

                // Objective pass/fail check no model involved in the decision.
                if (passes)
                {
                    return ValueTask.FromResult(LoopEvaluation.Stop());
                }

                return ValueTask.FromResult(LoopEvaluation.Continue(
                    $"That was {wordCount} words. Rewrite it to be {MaxWords} words or fewer "
                  + "while keeping the punch. Return only the slogan."));
            }),
            new LoopAgentOptions
            {
                MaxIterations = 5,

                // OnBehalfOfAuthorName: when the loop re-invokes the agent it sends
                // the feedback "on the caller's behalf". Stamping an author name lets
                // the wrapped agent tell those synthesized nudges apart from a real
                // user's turn.
                OnBehalfOfAuthorName = "harness",
            });

        AgentResponse lengthResult = await lengthHarness.RunAsync(
            "Create a slogan for an eco-friendly ride-sharing app that emphasizes speed and sustainability.");

        Console.WriteLine($"Final accepted slogan: {lengthResult}");

        Pause();

        // Part 3 - LLM as judge gate.
        // The most powerful gate, a SECOND model grades the first model's work
        // against natural language criteria, and only stops the loop when the work
        // passes. The worker keeps refining, the judge keeps critiquing a fully
        // automated draft/review cycle. The judge's own feedback is fed back to the
        // worker, so each round is informed by a specific critique.
        Console.WriteLine();
        Console.WriteLine("Part 3: Grade the work with an LLM as judge (AI judge gate)");
        Console.WriteLine("----------------------------------------------------------");

        AIAgent analyst = AgentFactory.CreateAgent(
            name: "Analyst",
            instructions:
                "You are a business analyst. Produce clear, well structured answers with "
              + "concrete, quantified recommendations.",
            model: AgentFactory.ToolCapableModel);

        // Captured by SessionCreatedCallback below - lets us grab the session the
        // loop creates so a follow-up conversation could continue after it finishes.
        AgentSession? capturedSession = null;

        AIJudgeLoopEvaluator judge = new(
            AgentFactory.CreateChatClient(AgentFactory.ToolCapableModel),
            new AIJudgeLoopEvaluatorOptions
            {
                Instructions =
                    "You are a demanding executive reviewer. Approve the answer ONLY if it fully "
                  + "satisfies every criterion otherwise explain precisely what is missing so it "
                  + "can be improved.",
                Criteria =
                [
                    "Recommends exactly three specific, actionable initiatives.",
                    "Each initiative includes a measurable target or KPI.",
                    "Calls out at least one concrete risk and how to mitigate it.",
                ],
            });

        LoopAgent judgedHarness = new(
            analyst,
            new LoggingLoopEvaluator("judge", judge),
            new LoopAgentOptions
            {
                MaxIterations = 4,

                // FreshContextPerIteration: instead of piling every rejected draft +
                // critique onto one ever-growing conversation, each retry restarts
                // from the ORIGINAL prompt plus an aggregated feedback log. The agent
                // rewrites from a clean slate each round instead of patching a messy
                // history - usually higher quality and cheaper on tokens.
                FreshContextPerIteration = true,

                // SessionCreatedCallback: the loop owns/creates the session(s). This
                // hook hands us the latest one so we could keep chatting with the
                // agent AFTER the loop finishes. We just capture it here.
                SessionCreatedCallback = (session, _) =>
                {
                    capturedSession = session;
                    return ValueTask.CompletedTask;
                },
            });

        AgentResponse judgedResult = await judgedHarness.RunAsync(
            "How can a small local bookstore grow revenue over the next year?");

        Console.WriteLine();
        Console.WriteLine(capturedSession is not null
            ? "(Captured the loop-created session via SessionCreatedCallback for follow-up chat.)"
            : "(No loop-created session was captured.)");

        Console.WriteLine();
        Console.WriteLine("Final approved analysis:");
        Console.WriteLine(judgedResult);

        Pause();

        // Part 4 - Combine gates: a C# rule AND an AI judge must BOTH approve.
        // A LoopAgent accepts several evaluators. They run in order after each
        // iteration and the FIRST one that wants to re invoke wins so the loop only
        // stops when EVERY gate is satisfied. That lets you enforce a cheap, exact,
        // deterministic rule (fast, free) alongside a nuanced quality judgement
        // (smart, but costs a model call). Here the briefing must:
        //   (a) fit under a hard length budget  [C# gate], AND
        //   (b) satisfy the executive criteria  [AI judge gate].
        Console.WriteLine();
        Console.WriteLine("Part 4: Combine gates C# rule AND AI judge must both pass");
        Console.WriteLine("----------------------------------------------------------");

        const int MaxChars = 1500;

        AIAgent briefingAgent = AgentFactory.CreateAgent(
            name: "BriefingWriter",
            instructions:
                "You are an executive briefing writer. Produce tight, high signal summaries "
              + "with concrete, quantified recommendations. Prefer short prose over long tables.",
            model: AgentFactory.ToolCapableModel);

        // Gate (a): a deterministic length budget, wrapped so we can see it fire.
        LoggingLoopEvaluator lengthGate = new("length", new DelegateLoopEvaluator((context, _) =>
        {
            int chars = context.LastResponse?.ToString().Length ?? 0;
            if (chars <= MaxChars)
            {
                return ValueTask.FromResult(LoopEvaluation.Stop());
            }

            return ValueTask.FromResult(LoopEvaluation.Continue(
                $"The briefing is {chars} characters, over the {MaxChars} character budget. "
              + "Condense it while keeping every recommendation and its KPI."));
        }));

        // Gate (b): the same executive judge as Part 3, also wrapped for logging.
        LoggingLoopEvaluator qualityGate = new("judge", new AIJudgeLoopEvaluator(
            AgentFactory.CreateChatClient(AgentFactory.ToolCapableModel),
            new AIJudgeLoopEvaluatorOptions
            {
                Instructions =
                    "You are a demanding executive reviewer. Approve ONLY if every criterion is "
                  + "fully met, otherwise say precisely what is missing.",
                Criteria =
                [
                    "Recommends exactly three specific, actionable initiatives.",
                    "Each initiative includes a measurable target or KPI.",
                    "Calls out at least one concrete risk and how to mitigate it.",
                ],
            }));

        LoopAgent combinedHarness = new(
            briefingAgent,
            new LoopEvaluator[] { lengthGate, qualityGate },
            new LoopAgentOptions
            {
                MaxIterations = 5,

                // ExcludeOnBehalfOfMessages: hide the loop's internal feedback nudges
                // from the surfaced output so the aggregated transcript contains only
                // the agent's own briefings, not the harness's book-keeping messages.
                ExcludeOnBehalfOfMessages = true,

                // Name the synthesized feedback so the wrapped agent can distinguish
                // harness guidance from a genuine user request.
                OnBehalfOfAuthorName = "reviewer",
            });

        AgentResponse combinedResult = await combinedHarness.RunAsync(
            "Write an executive briefing on how a small local bookstore can grow revenue next year.");

        Console.WriteLine();
        Console.WriteLine(
            $"Final briefing ({combinedResult.ToString().Length} chars, within {MaxChars} budget "
          + "and judge approved):");
        Console.WriteLine(combinedResult);

        Console.WriteLine();
        Console.WriteLine("Takeaway: a harness turns a one shot agent into a self correcting one ");
        Console.WriteLine("swap or STACK gates (marker / C# rule / AI judge) to control what 'done'");
        Console.WriteLine("means the loop only stops when every gate agrees, all behind one RunAsync call.");
    }

    /// <summary>
    /// Pauses the walkthrough until the user presses Enter, so each part can be read
    /// before the next one runs (instead of the whole lesson scrolling by at once).
    /// </summary>
    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press Enter for the next part...");
        Console.ReadLine();
        Console.WriteLine();
    }
}

/// <summary>
/// A tiny decorator that wraps any <see cref="LoopEvaluator"/> and logs, for each
/// iteration, whether the wrapped gate ACCEPTED (stop) or asked for a RETRY
/// (re invoke) plus any feedback it produced. It changes no behaviour, it just
/// makes the otherwise invisible loop observable in the console.
/// </summary>
internal sealed class LoggingLoopEvaluator(string label, LoopEvaluator inner) : LoopEvaluator
{
    public override async ValueTask<LoopEvaluation> EvaluateAsync(
        LoopContext context, CancellationToken cancellationToken = default)
    {
        LoopEvaluation evaluation = await inner.EvaluateAsync(context, cancellationToken);

        string decision = evaluation.ShouldReinvoke ? "RETRY" : "ACCEPT";
        Console.WriteLine($"   [gate:{label}] iteration {context.Iteration}: {decision}");

        if (evaluation.ShouldReinvoke && !string.IsNullOrWhiteSpace(evaluation.Feedback))
        {
            // Keep the feedback preview short so the console stays readable.
            string feedback = evaluation.Feedback!.Replace(Environment.NewLine, " ");
            if (feedback.Length > 160)
            {
                feedback = feedback[..160] + "...";
            }

            Console.WriteLine($"   [gate:{label}] feedback -> {feedback}");
        }

        return evaluation;
    }
}
