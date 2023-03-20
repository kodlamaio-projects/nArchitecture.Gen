namespace Core.Application.Commands;

public abstract class BaseStreamCommandResponse
{
    public string CurrentStatusMessage { get; set; }
    public string? OutputMessage { get; set; }
    public string? LastOperationMessage { get; set; }

    protected BaseStreamCommandResponse()
    {
        CurrentStatusMessage = string.Empty;
    }
}
