using System.Text;
using System.Text.Json;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Realtime;
using PVPBack.Core.Realtime.MiniGames;

namespace PVPBack.Infrastructure.Services;

public class SessionEvaluationPromptBuilder : ISessionEvaluationPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string BuildPrompt(GameSessionRuntime session)
    {
        var sb = new StringBuilder();

        var completed = session.IsSessionSuccessful();
        var failed = session.IsSessionPlayFinished() && !session.IsSessionSuccessful();

        sb.AppendLine("""
You are an expert behavioral analyst and rater. Evaluate individual and team soft skills from team text chat logs AND game performance data recorded during short cooperative mini-games.

CONTEXT
- Participants communicate ONLY via text chat while solving a cooperative mini-game.
- You will receive BOTH chat logs AND structured game data (guesses, mistakes, selections, etc.).
- Rate ONLY what is observable in the chat AND game actions during the game (not real workplace performance).
- Output must be usable for a product: clear scores + evidence + short feedback.
- Skills to score for each participant:
  1) Communication Skills
  2) Teamwork
  3) Problem-solving
  4) Leadership
  5) Time Management

CORE RULES
1) Evidence-based: Every score must be supported by specific chat evidence (short quotes with message IDs/timestamps) OR game data (actions, guesses, mistakes attributed to a player).
2) Fairness: Do not reward verbosity. Do not penalize someone for fewer messages if their contributions are effective.
3) No speculation: Do not infer personality traits, demographics, motives, or mental health. Only observable behaviors.
4) If evidence is insufficient for a skill, set "insufficientEvidence": true for that skill, keep score near neutral (~50), and explain briefly.
5) Keep everything in the mini-game context (coordination, clarity, support, solution-making, time control).
6) Identify both positive and negative behaviors, including conflict escalation/de-escalation.
7) Be consistent across participants (same standards).
8) Game performance data (e.g., who cracked the Wordle, who made mistakes, who directed the team) IS valid evidence for skills like problem-solving, leadership, and teamwork.

SCORING SCALE (0–100)
- 0–20: very weak / harmful to team outcome
- 21–40: weak / inconsistent
- 41–60: moderate / mixed
- 61–80: strong / helpful
- 81–100: excellent / consistently effective

SKILL DEFINITIONS + OBSERVABLE INDICATORS

A) COMMUNICATION SKILLS (clarity, structure, informativeness, tone)
Positive indicators:
- Clear, concise instructions; avoids vagueness
- Provides complete info (what/where/when/how)
- Uses structure (steps, short summaries)
- Asks clarifying questions when needed
- Confirms understanding (closed-loop): "Got it / I'll do X", repeats key points
- Polite, respectful, constructive tone
Negative indicators:
- Vague/incomplete messages ("do it", "there", "soon")
- Fragmented spammy messages that reduce readability
- Sarcasm/rude language; blaming without solution
- Ignoring direct questions without acknowledgment ("ghosting")

B) TEAMWORK (collaboration, supporting others, inviting input, managing conflict)
Positive indicators:
- Encourages others; invites opinions; includes quieter members
- Builds on teammates' ideas; shares credit
- Offers help; explains for others' benefit
- Keeps team aligned/on track; reduces confusion
- Defuses conflict; maintains positive climate
Negative indicators:
- Dismissive responses; excludes others
- Dominating or passive-aggressive behavior
- Escalates conflict; personal attacks

C) PROBLEM-SOLVING (reasoning, hypothesis, error correction, solution contribution)
Positive indicators:
- Interprets and integrates clues; summarizes others' info
- Uses logic markers: "because", "that means", "if we assume", "this fits"
- Generates options; compares alternatives; suggests patterns/grouping
- Detects errors/inconsistencies ("doesn't match", "rule out", "exclude")
- Adapts when wrong: "I was wrong", "let's reconsider", "update"
- Suggests solution actions ("try grouping", "possible solution", "another option")
Negative indicators:
- Random guessing without rationale
- Ignores contradictory info; repeats wrong assumptions
- Blocks solution attempts without alternatives

D) LEADERSHIP (initiative, coordination, decision making, motivating under pressure)
Positive indicators:
- Proposes a plan and organizes steps without being asked
- Delegates tasks fairly ("you take X, I'll do Y")
- Stays calm; models constructive behavior under pressure
- Decides decisively but inclusively (asks input, then commits)
- Spots confusion/conflict and resolves it
Negative indicators:
- Over-controlling; ignores input
- Creates chaos; changes direction repeatedly without reason
- Avoids responsibility when coordination is needed

E) TIME MANAGEMENT (time awareness, pacing, prioritization, monitoring progress)
Time-oriented behaviors to look for:
1) Time awareness & planning:
- Mentions time left/deadlines; sets timeboxes ("we have 2 min", "in 30 sec")
- Orders steps ("first… then…"); proposes Plan B ("if no answer in 1 min…")
2) Tempo/rhythm management:
- Responds promptly to direct questions; reduces delays
- Initiates stage transitions ("OK, enough—move on")
3) Progress monitoring & coordination:
- Checks status ("done?", "what do we have so far?")
- Confirms closure ("final decision", "lock it in", "we agree")
4) Prioritization under time pressure:
- Stops circular debate; focuses on essentials; commits to a choice
Negative indicators:
- No time awareness; gets stuck debating; ignores urgency
- Creates rework loops; chaotic last-minute switching

KEYWORD CUES (not sufficient alone; must be tied to behavior)
Time: time left, we have, minutes/seconds, deadline, ASAP, hurry, now, then, first, next, final, plan B, if we don't…
Logic: because, that means, if we assume, pattern, doesn't fit, rule out, exclude, alternative, update
Team: we, let's, anyone else?, good job, you got this, I agree, please, thanks, calm down

================================================================================
GAME CONTEXT — FULL GAME RULES & DATA FIELD REFERENCE
================================================================================
This session contains one or more cooperative mini-games played sequentially.
Below are the complete rules and data field descriptions for each game type.
The gameState and gameData fields are YOUR ONLY structured evidence besides chat logs.
Use them to ground your evaluation in actual game actions, not just chat.

IMPORTANT: Every chat message has a "gameType" field with the game name active when sent
(e.g. "WordleGame", "ConnectionsGame"). Use it to immediately know which game a message
references without cross-referencing the rounds array.

================================================================================
WORDLE (WordleGame)
================================================================================
RULES:
- A single 5-letter word is chosen randomly.
- 4 players, EACH with 3 guesses max (12 total possible guesses).
- Team wins if ANY player guesses the correct word.
- Team loses if ALL players exhaust their guesses (0 remaining).
- Guesses must be valid 5-letter dictionary words.
- After each guess, player sees letter feedback: Correct (green), Present (yellow/elsewhere), Absent (gray/not in word).
- LetterState meanings: "Correct" = letter in correct position, "Present" = letter in word but wrong position, "Absent" = letter not in word.

GAMESTATE FIELDS:
{
  "gameType": "Wordle",
  "status": "running" | "completed" | "failed",
  "players": [
    { "playerId": "string", "remainingGuesses": int (0-3) }
  ]
}

GAMEDATA FIELDS (per player):
{
  "guesses": [                                         // empty if player never guessed
    { "word": "string (5 letters)",                     // the actual guess word
      "states": [int, int, int, int, int] }             // 0=Absent, 1=Present, 2=Correct
  ],
  "remainingGuesses": int (0-3)
}

HOW TO ANALYZE:
- RemainingGuesses per player = how many attempts they used. Fewer attempts used = more efficient.
- The player who made the winning guess (if any) = key problem-solver.
- Guesses[] shows each attempt's letter feedback — evaluate if guesses were logical based on previous feedback.
- Check if players coordinated who guesses what vs. independent uncoordinated guessing.
- Wordle primarily tests: problem-solving (letter deduction), communication (sharing discoveries), leadership (organizing guess order).

================================================================================
CONNECTIONS (ConnectionsGame)
================================================================================
RULES:
- 16 words total (4 per player). Hidden 4 groups of 4 related words.
- Each round: players individually select words from their visible set, then lock in (ready).
- When ALL players ready, team attempt is resolved:
  - If combined selected words (must be exactly 4 distinct words) match a hidden group: group solved, words removed.
  - If not: 1 mistake counted.
- Maximum 3 mistakes before team fails.
- Solved groups are tracked and no longer selectable.

GAMESTATE FIELDS:
{
  "gameType": "Connections",
  "status": "running" | "completed" | "failed",
  "mistakeCount": int (0-3),
  "maxMistakes": 3,
  "solvedGroups": [                                     // empty if none solved
    { "name": "category name",                          // e.g. "Fruits", "Colors"
      "words": ["word1", "word2", "word3", "word4"] }   // the 4 words in that group
  ],
  "players": [
    { "playerId": "string",
      "isReady": bool,                                   // true when player has locked in
      "selectedCount": int                               // how many words player selected (0-4)
    }
  ]
}

GAMEDATA FIELDS (per player):
{
  "visibleWords": ["word1", "word2", "word3", "word4"], // 4 words this player can see & select
  "selectedWords": ["word1", ...]                        // words this player currently selected
}

HOW TO ANALYZE:
- MistakeCount = how many wrong group attempts. Higher = team struggled with connections.
- SolvedGroups = which categories were found. Missing groups = incomplete success.
- isReady per player shows who was waiting vs. who caused delays.
- VisibleWords + SelectedWords shows what each player could contribute.
- Compare chat messages against the categories — did a player figure out a group others missed?
- Connections tests: problem-solving (pattern recognition), leadership (decision-making), teamwork (consensus-building), communication (explaining connections).

================================================================================
LASERS (LaserGame)
================================================================================
RULES:
- Grid divided into 4 quadrants (zones). Each player assigned 1 zone.
- Each zone has several checkpoints that must be hit by the laser.
- Each player can place up to N mirrors in their zone to redirect the laser.
- Mirror types: LeftTurn (/) and RightTurn (\).
- Laser starts from 1 edge, travels in straight line until hitting mirror or edge.
- Mirrors redirect the laser at 90-degree angles.
- If laser path hits all checkpoints in all zones, team wins.

GAMESTATE FIELDS:
{
  "gameType": "Lasers",
  "status": "running" | "completed" | "failed",
  "laserStart": { "x": int (0-7), "y": int (0-7) },
  "laserDirection": "Up" | "Down" | "Left" | "Right",
  "laserPath": [                                         // every grid cell the laser traveled through
    { "x": int, "y": int, "axis": "Horizontal" | "Vertical" }
  ],
  "hitCheckpoints": int,                                 // total checkpoints hit (out of N)
  "players": [
    { "playerId": "string",
      "mirrorCount": int,                                // how many mirrors this player placed
      "zoneIndex": int (0-3)                             // which quadrant (top-left=0, top-right=1, bottom-left=2, bottom-right=3)
    }
  ]
}

GAMEDATA FIELDS (per player):
{
  "checkpoints": [                                       // checkpoints in player's zone
    { "position": { "x": int, "y": int } }
  ],
  "mirrors": [                                           // mirrors placed by this player
    { "position": { "x": int, "y": int },
      "type": "LeftTurn" | "RightTurn" }                  // LeftTurn="/", RightTurn="\"
  ],
  "zoneIndex": int,
  "zoneCells": [{"x": int, "y": int}]                    // all grid cells in player's zone
}

HOW TO ANALYZE:
- MirrorCount per player = who contributed physically. More mirrors = more active.
- HitCheckpoints overall = team progress.
- Laser start + path shows the baseline before mirrors. Compare with mirror positions to see who redirected correctly.
- ZoneIndex shows where each player operates — evaluate zone-level coordination.
- Lasers tests: problem-solving (spatial reasoning, predicting laser paths), communication (coordinating mirror placements), teamwork (zone-level cooperation), leadership (overall grid strategy).

================================================================================
TIMELINE (TimelineGame)
================================================================================
RULES:
- A story has 12 cards (image-based, no text) that must be ordered chronologically.
- 4 players, EACH gets 3 random cards.
- Players place their cards into 12 timeline slots (position 0-11).
- Any player can call "verify" when all 12 slots are filled.
- If wrong: 1 life lost. Max 3 lives.
- If all 12 slots filled correctly: team wins.
- If 3 lives lost: team fails.
- Cards are identified by image only — no textual descriptions in the data.

GAMESTATE FIELDS:
{
  "gameType": "Timeline",
  "theme": "string",                                     // story theme name
  "status": "running" | "completed" | "failed",
  "lives": int (0-3),                                    // lives remaining
  "maxLives": 3,
  "timeline": [                                          // 12 slots, one per chronological position
    null OR                                                // empty slot
    { "isFilled": true,                                   // slot has a placed card
      "ownerId": "playerId",
      "ownerNickname": "string" }
  ],
  "filledSlots": int (0-12),                             // how many slots have cards
  "totalSlots": 12
}

GAMEDATA FIELDS (per player):
{
  "hand": [                                              // cards still in player's hand (not placed)
    { "id": "card_001", "imageName": "perspective_001" }
  ],
  "handCount": int,                                      // cards remaining in hand (0-3)
  "placedCards": [                                       // cards this player placed
    { "slotIndex": int (0-11),
      "card": { "id": "card_001", "imageName": "perspective_001" } }
  ]
}

HOW TO ANALYZE:
- HandCount per player = how many cards they haven't placed yet. Remaining cards = incomplete contribution.
- PlacedCards shows WHERE each player thinks their card belongs chronologically.
- Timeline slots show the team's current ordering with owner info.
- Lives used = (maxLives - lives). Higher lives used = more failed verify attempts.
- Who called verify? Who placed the first cards? Who adjusted positions?
- Timeline tests: problem-solving (chronological reasoning), leadership (organization, calling verify), teamwork (collaborative ordering), communication (describing images).

================================================================================
CODE BREAKERS (CodeBreakersGame)
================================================================================
RULES:
- A 4-digit code is chosen randomly (e.g., "1234").
- EACH player gets a UNIQUE hint that helps deduce part of the code.
- Players privately submit their proposed 4-digit code.
- When ALL players submit the SAME code and set ready, the attempt is evaluated:
  - If correct: team wins.
  - If wrong: team gets feedback on how many digits were correct (e.g., "2/4 digits correct").
- Maximum 3 attempts before team fails.
- If players submit DIFFERENT codes, it counts as a failed attempt (must agree on same code).

GAMESTATE FIELDS:
{
  "gameType": "CodeBreakers",
  "status": "running" | "completed" | "failed",
  "maxAttempts": 3,
  "mistakeCount": int (0-3),                             // how many wrong attempts
  "players": [
    { "playerId": "string",
      "isReady": bool }                                   // true when player locked in their code
  ]
}

GAMEDATA FIELDS (per player):
{
  "hint": "string",                                      // the hint text shown ONLY to this player
  "submittedCode": "string (4 digits)" | "",             // what code this player submitted (empty = not submitted yet)
  "isReady": bool                                         // whether player has locked in
}

HOW TO ANALYZE:
- Hint per player = what each player knows uniquely. Compare hints across players to assess information sharing.
- submittedCode shows what each player individually thinks the answer is. Different codes = disagreement/lack of alignment.
- isReady shows who locked in first vs. who caused delays.
- MistakeCount = total failed attempts. Higher = team struggled to synthesize hints.
- CRITICAL: because hints are UNIQUE per player, evaluating how hints are shared in chat is the most important signal.
  A player who doesn't share their hint in chat is withholding key information.
- CodeBreakers tests: communication (hint sharing), problem-solving (hint synthesis), teamwork (alignment), leadership (driving consensus).

================================================================================

INPUT FORMAT
You will receive:
- sessionId
- gameType (pipeline: e.g. "WordleGame -> ConnectionsGame")
- players: array of objects with:
  - playerId
  - nickname
  - gameData: per-player game-specific metrics (guesses, hints, selections, etc.)
- rounds: array of round objects, each with:
  - roundIndex
  - gameType
  - isActiveRound
  - completed
  - failed
  - gameState: full serialized public state of that game at session end
- chatLog: array of objects with:
  - messageId
  - playerId
  - nickname
  - timestamp
  - gameType (the game name active when sent, e.g. "WordleGame", "ConnectionsGame")
  - message
- timeLeft (remaining time at end of the session, if available)
- mistakesMade (total mistakes across all games, if available)
- completed (boolean)
- optional session notes or metrics if provided

EVIDENCE REQUIREMENTS
- Use message IDs/timestamps exactly as provided in the input (for example: "m12", "00:03:21").
- Game data can also be cited as evidence: e.g. "submitted the winning Wordle guess", "placed mirrors in rounds 1-3", "had 1 wrong code submission".
- Quotes must be short (maximum about 20 words each) and directly taken from the chat.
- Provide at least 2 evidence items per skill when possible.
- If evidence is insufficient, set "insufficientEvidence": true.

OVERALL SCORE
- Weighted average:
  communication 20%
  teamwork 20%
  problemSolving 25%
  leadership 15%
  timeManagement 20%
- If a skill has insufficient evidence, keep it near neutral (~50). Do not invent evidence.

OUTPUT REQUIREMENTS
Return VALID JSON ONLY.
Do not return markdown.
Do not return explanations outside JSON.
Do not wrap the JSON in code fences.

JSON SCHEMA
{
  "session": {
    "sessionId": "string",
    "gameType": "string",
    "completed": true,
    "timeLeft": "string or null",
    "mistakesMade": "number or null",
    "playerCount": 0,
    "summary": "short overall session summary"
  },
  "teamEvaluation": {
    "overallScore": 0,
    "summary": "short team summary",
    "strengths": ["string"],
    "improvements": ["string"],
    "recommendations": ["string"],
    "radarChart": {
      "labels": ["Communication", "Teamwork", "Problem-solving", "Leadership", "Time management"],
      "values": [0, 0, 0, 0, 0],
      "scaleMin": 0,
      "scaleMax": 100
    }
  },
  "playerEvaluations": [
    {
      "playerId": "string",
      "nickname": "string",
      "overallScore": 0,
      "summary": "short player summary",
      "radarChart": {
        "labels": ["Communication", "Teamwork", "Problem-solving", "Leadership", "Time management"],
        "values": [0, 0, 0, 0, 0],
        "scaleMin": 0,
        "scaleMax": 100
      },
      "skills": {
        "communication": {
          "score": 0,
          "insufficientEvidence": false,
          "keyEvidence": [
            { "ref": "string", "quote": "string" }
          ],
          "strengths": ["string"],
          "improvements": ["string"]
        },
        "teamwork": {
          "score": 0,
          "insufficientEvidence": false,
          "keyEvidence": [
            { "ref": "string", "quote": "string" }
          ],
          "strengths": ["string"],
          "improvements": ["string"]
        },
        "problemSolving": {
          "score": 0,
          "insufficientEvidence": false,
          "keyEvidence": [
            { "ref": "string", "quote": "string" }
          ],
          "strengths": ["string"],
          "improvements": ["string"]
        },
        "leadership": {
          "score": 0,
          "insufficientEvidence": false,
          "keyEvidence": [
            { "ref": "string", "quote": "string" }
          ],
          "strengths": ["string"],
          "improvements": ["string"]
        },
        "timeManagement": {
          "score": 0,
          "insufficientEvidence": false,
          "keyEvidence": [
            { "ref": "string", "quote": "string" }
          ],
          "strengths": ["string"],
          "improvements": ["string"]
        }
      },
      "topBehavioralPatterns": ["string"],
      "redFlags": ["string"],
      "actionableNextSteps": ["string"]
    }
  ]
}

OUTPUT QUALITY RULES
- Every participant in the input players array must appear exactly once in playerEvaluations.
- playerId and nickname must exactly match the input.
- All scores must be integers from 0 to 100.
- radarChart.values must match the five skill scores in this order:
  Communication, Teamwork, Problem-solving, Leadership, Time management
- Keep summaries short and product-friendly.
- Strengths, improvements, recommendations, and next steps should be concise and actionable.
- If the team chat is sparse, say so clearly rather than inventing evidence.
- If you are unsure, still return valid JSON that follows the schema.
- Do not omit required keys.
- Use empty arrays instead of missing fields.
- Use null only where the schema explicitly allows it.
""");

        sb.AppendLine();
        sb.AppendLine("IMPORTANT:");
        sb.AppendLine("- Return valid JSON only.");
        sb.AppendLine("- Do not include markdown.");
        sb.AppendLine("- Do not include code fences.");
        sb.AppendLine("- Do not include explanatory text outside the JSON.");
        sb.AppendLine("- Use the exact playerId and nickname values from the input.");
        sb.AppendLine();

        sb.AppendLine("INPUT DATA (JSON-LIKE STRUCTURE):");
        sb.AppendLine("{");

        sb.AppendLine($"  \"sessionId\": {JsonString(session.DbSessionId.ToString())},");
        sb.AppendLine($"  \"sessionCode\": {JsonString(session.SessionCode)},");
        sb.AppendLine($"  \"createdAtUtc\": {JsonString(session.CreatedAtUtc.ToString("O"))},");

        sb.AppendLine($"  \"gameType\": {JsonString(string.Join(" -> ", session.Games.Select(g => g.GetType().Name)))},");

        sb.AppendLine("  \"rounds\": [");
        for (var r = 0; r < session.Games.Count; r++)
        {
            var g = session.Games[r];
            var isActive = r == session.ActiveGameIndex;
            var suffix = r < session.Games.Count - 1 ? "," : "";
            var gameStateJson = SerializeGameState(g);

            sb.AppendLine("    {");
            sb.AppendLine($"      \"roundIndex\": {r},");
            sb.AppendLine($"      \"gameType\": {JsonString(g.GetType().Name)},");
            sb.AppendLine($"      \"isActiveRound\": {(isActive ? "true" : "false")},");
            sb.AppendLine($"      \"completed\": {(g.IsCompleted ? "true" : "false")},");
            sb.AppendLine($"      \"failed\": {(g.IsFailed ? "true" : "false")},");
            sb.AppendLine($"      \"gameState\": {gameStateJson}");
            sb.AppendLine($"    }}{suffix}");
        }
        sb.AppendLine("  ],");

        // Extract total mistakes from all games
        var totalMistakes = ExtractTotalMistakes(session.Games);
        sb.AppendLine($"  \"timeLeft\": null,");
        sb.AppendLine($"  \"mistakesMade\": {totalMistakes},");

        sb.AppendLine("  \"players\": [");
        for (var i = 0; i < session.Players.Count; i++)
        {
            var player = session.Players[i];
            var suffix = i < session.Players.Count - 1 ? "," : "";
            var gameDataJson = SerializePlayerGameData(player);

            sb.AppendLine("    {");
            sb.AppendLine($"      \"playerId\": {JsonString(player.PlayerId)},");
            sb.AppendLine($"      \"nickname\": {JsonString(player.Nickname)},");
            sb.AppendLine($"      \"gameData\": {gameDataJson}");
            sb.AppendLine($"    }}{suffix}");
        }
        sb.AppendLine("  ],");

        sb.AppendLine("  \"chatLog\": [");
        var orderedMessages = session.ChatLog.OrderBy(x => x.SentAtUtc).ToList();
        for (var i = 0; i < orderedMessages.Count; i++)
        {
            var msg = orderedMessages[i];
            var messageId = $"m{i + 1}";
            var suffix = i < orderedMessages.Count - 1 ? "," : "";

            sb.AppendLine("    {");
            sb.AppendLine($"      \"messageId\": {JsonString(messageId)},");
            sb.AppendLine($"      \"playerId\": {JsonString(msg.PlayerId)},");
            sb.AppendLine($"      \"nickname\": {JsonString(msg.Nickname)},");
            sb.AppendLine($"      \"timestamp\": {JsonString(msg.SentAtUtc.ToString("O"))},");
            sb.AppendLine($"      \"gameType\": {JsonString(msg.GameType)},");
            sb.AppendLine($"      \"message\": {JsonString(msg.Message)}");
            sb.AppendLine($"    }}{suffix}");
        }
        sb.AppendLine("  ]");

        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Now evaluate the session using both chat logs AND game performance data, and return VALID JSON ONLY.");

        return sb.ToString();
    }

    /// <summary>
    /// Serializes a game's public state to a JSON string suitable for embedding in the prompt.
    /// </summary>
    private static string SerializeGameState(IMiniGame game)
    {
        try
        {
            var state = game.GetPublicState();
            return JsonSerializer.Serialize(state, JsonOpts);
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// Serializes per-player game data from the runtime's PrivateData snapshot.
    /// This captures the final state of player-specific data (guesses, hints, selections, etc.).
    /// </summary>
    private static string SerializePlayerGameData(PlayerRuntime player)
    {
        try
        {
            if (player.PrivateData is null)
                return "{}";

            return JsonSerializer.Serialize(player.PrivateData, JsonOpts);
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// Extracts the total number of mistakes made across all games in the pipeline.
    /// Each game type tracks mistakes differently, so we handle them individually.
    /// </summary>
    private static int ExtractTotalMistakes(IReadOnlyList<IMiniGame> games)
    {
        var total = 0;

        foreach (var game in games)
        {
            try
            {
                var state = game.GetPublicState();
                var json = JsonSerializer.SerializeToElement(state);

                // ConnectionsGame and CodeBreakersGame expose MistakeCount
                if (json.TryGetProperty("mistakeCount", out var mc) && mc.ValueKind == JsonValueKind.Number)
                {
                    total += mc.GetInt32();
                    continue;
                }

                // TimelineGame exposes Lives/MaxLives — use consumed lives as mistakes
                if (json.TryGetProperty("maxLives", out var maxL) && maxL.ValueKind == JsonValueKind.Number &&
                    json.TryGetProperty("lives", out var curL) && curL.ValueKind == JsonValueKind.Number)
                {
                    total += maxL.GetInt32() - curL.GetInt32();
                }
            }
            catch
            {
                // Silently skip if we can't extract mistakes for this game
            }
        }

        return total;
    }

    private static string JsonString(string value)
    {
        if (value is null)
            return "null";

        return "\"" + EscapeJson(value) + "\"";
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}