namespace Application.Features.Generate.Commands;

/// <summary>Files reported by a generate command when it completes.</summary>
public interface IGeneratedFilesResponse
{
    ICollection<string>? NewFilePathsResult { get; }
    ICollection<string>? UpdatedFilePathsResult { get; }
}
