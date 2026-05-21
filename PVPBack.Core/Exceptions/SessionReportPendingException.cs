namespace PVPBack.Core.Exceptions;

public sealed class SessionReportPendingException(string sessionCode) : Exception("AI report is still being generated.")
{
    public string SessionCode { get; } = sessionCode;
}