using Core.Application.Commands;

namespace Application.Features.Upgrade.Commands.UpgradeToV2;

public class UpgradedProjectResponse : BaseStreamCommandResponse
{
    public ICollection<string>? ChangedFilePathsResult { get; set; }
    public string? ReportFilePath { get; set; }
    public int ManualWorkCount { get; set; }
    public bool? BuildSucceeded { get; set; }
}
