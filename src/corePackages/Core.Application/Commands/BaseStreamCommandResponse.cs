namespace Core.Application.Commands;

public abstract class BaseStreamCommandResponse
{
    public string CurrentStatusMessage { get; set; }
    public string? OutputMessage { get; set; }
    public string? LastOperationMessage { get; set; }

    /// <summary>Steps that were skipped or need attention; the operation still succeeded.</summary>
    public ICollection<string> Warnings { get; } = new List<string>();

    protected BaseStreamCommandResponse()
    {
        CurrentStatusMessage = string.Empty;
    }
}
