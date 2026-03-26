using PVPBack.Core.Realtime;

namespace PVPBack.Core.Interfaces;

public interface ISessionEvaluationPromptBuilder
{
    string BuildPrompt(GameSessionRuntime session);
}