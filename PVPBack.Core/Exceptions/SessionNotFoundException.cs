namespace PVPBack.Core.Exceptions;

public sealed class SessionNotFoundException(string sessionCode) : Exception($"Session '{sessionCode}' was not found.")
{
    public string SessionCode { get; } = sessionCode;
}