using System.Text;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Realtime;

namespace PVPBack.Infrastructure.Services;

public class SessionEvaluationPromptBuilder : ISessionEvaluationPromptBuilder
{
    public string BuildPrompt(GameSessionRuntime session)
    {
        var sb = new StringBuilder();

        var completed = session.IsSessionSuccessful();
        var failed = session.IsSessionPlayFinished() && !session.IsSessionSuccessful();

        sb.AppendLine("""
You are an expert behavioral analyst and rater. Evaluate individual and team soft skills from team text chat logs recorded during short cooperative mini-games.

CONTEXT
- Participants communicate ONLY via text chat while solving a cooperative mini-game.
- Rate ONLY what is observable in the chat during the game (not real workplace performance).
- Output must be usable for a product: clear scores + evidence + short feedback.
- Skills to score for each participant:
  1) Communication Skills
  2) Teamwork
  3) Problem-solving
  4) Leadership
  5) Time Management

CORE RULES
1) Evidence-based: Every score must be supported by specific chat evidence (short quotes with message IDs/timestamps).
2) Fairness: Do not reward verbosity. Do not penalize someone for fewer messages if their contributions are effective.
3) No speculation: Do not infer personality traits, demographics, motives, or mental health. Only observable behaviors.
4) If evidence is insufficient for a skill, set "insufficientEvidence": true for that skill, keep score near neutral (~50), and explain briefly.
5) Keep everything in the mini-game context (coordination, clarity, support, solution-making, time control).
6) Identify both positive and negative behaviors, including conflict escalation/de-escalation.
7) Be consistent across participants (same standards).

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
- Confirms understanding (closed-loop): “Got it / I’ll do X”, repeats key points
- Polite, respectful, constructive tone
Negative indicators:
- Vague/incomplete messages (“do it”, “there”, “soon”)
- Fragmented spammy messages that reduce readability
- Sarcasm/rude language; blaming without solution
- Ignoring direct questions without acknowledgment (“ghosting”)

B) TEAMWORK (collaboration, supporting others, inviting input, managing conflict)
Positive indicators:
- Encourages others; invites opinions; includes quieter members
- Builds on teammates’ ideas; shares credit
- Offers help; explains for others’ benefit
- Keeps team aligned/on track; reduces confusion
- Defuses conflict; maintains positive climate
Negative indicators:
- Dismissive responses; excludes others
- Dominating or passive-aggressive behavior
- Escalates conflict; personal attacks

C) PROBLEM-SOLVING (reasoning, hypothesis, error correction, solution contribution)
Positive indicators:
- Interprets and integrates clues; summarizes others’ info
- Uses logic markers: “because”, “that means”, “if we assume”, “this fits”
- Generates options; compares alternatives; suggests patterns/grouping
- Detects errors/inconsistencies (“doesn’t match”, “rule out”, “exclude”)
- Adapts when wrong: “I was wrong”, “let’s reconsider”, “update”
- Suggests solution actions (“try grouping”, “possible solution”, “another option”)
Negative indicators:
- Random guessing without rationale
- Ignores contradictory info; repeats wrong assumptions
- Blocks solution attempts without alternatives

D) LEADERSHIP (initiative, coordination, decision making, motivating under pressure)
Positive indicators:
- Proposes a plan and organizes steps without being asked
- Delegates tasks fairly (“you take X, I’ll do Y”)
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
- Mentions time left/deadlines; sets timeboxes (“we have 2 min”, “in 30 sec”)
- Orders steps (“first… then…”); proposes Plan B (“if no answer in 1 min…”)
2) Tempo/rhythm management:
- Responds promptly to direct questions; reduces delays
- Initiates stage transitions (“OK, enough—move on”)
3) Progress monitoring & coordination:
- Checks status (“done?”, “what do we have so far?”)
- Confirms closure (“final decision”, “lock it in”, “we agree”)
4) Prioritization under time pressure:
- Stops circular debate; focuses on essentials; commits to a choice
Negative indicators:
- No time awareness; gets stuck debating; ignores urgency
- Creates rework loops; chaotic last-minute switching

KEYWORD CUES (not sufficient alone; must be tied to behavior)
Time: time left, we have, minutes/seconds, deadline, ASAP, hurry, now, then, first, next, final, plan B, if we don’t…
Logic: because, that means, if we assume, pattern, doesn’t fit, rule out, exclude, alternative, update
Team: we, let’s, anyone else?, good job, you got this, I agree, please, thanks, calm down

INPUT FORMAT
You will receive:
- sessionId
- gameType
- players: array of objects with:
  - playerId
  - nickname
- chatLog: array of objects with:
  - messageId
  - playerId
  - nickname
  - timestamp
  - message
- timeLeft (remaining time at end of the game, if available)
- mistakesMade (count and/or short description if available)
- completed (boolean)
- optional session notes or metrics if provided

EVIDENCE REQUIREMENTS
- Use message IDs/timestamps exactly as provided in the input (for example: "m12", "00:03:21").
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
          sb.AppendLine("    {");
          sb.AppendLine($"      \"roundIndex\": {r},");
          sb.AppendLine($"      \"gameType\": {JsonString(g.GetType().Name)},");
          sb.AppendLine($"      \"isActiveRound\": {(isActive ? "true" : "false")},");
          sb.AppendLine($"      \"completed\": {(g.IsCompleted ? "true" : "false")},");
          sb.AppendLine($"      \"failed\": {(g.IsFailed ? "true" : "false")}");
          sb.AppendLine($"    }}{suffix}");
        }
        sb.AppendLine("  ],");
        sb.AppendLine($"  \"timeLeft\": null,");
        sb.AppendLine($"  \"mistakesMade\": null,");
        

        sb.AppendLine("  \"players\": [");
        for (var i = 0; i < session.Players.Count; i++)
        {
            var player = session.Players[i];
            var suffix = i < session.Players.Count - 1 ? "," : "";

            sb.AppendLine("    {");
            sb.AppendLine($"      \"playerId\": {JsonString(player.PlayerId)},");
            sb.AppendLine($"      \"nickname\": {JsonString(player.Nickname)}");
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
            sb.AppendLine($"      \"message\": {JsonString(msg.Message)}");
            sb.AppendLine($"    }}{suffix}");
        }
        sb.AppendLine("  ]");

        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Now evaluate the session and return VALID JSON ONLY.");

        return sb.ToString();
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