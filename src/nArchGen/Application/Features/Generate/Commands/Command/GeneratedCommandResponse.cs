using Core.Application.Commands;

namespace Application.Features.Generate.Commands.Command;

public class GeneratedCommandResponse : BaseStreamCommandResponse, IGeneratedFilesResponse
{
    public ICollection<string>? NewFilePathsResult { get; set; }
    public ICollection<string>? UpdatedFilePathsResult { get; set; }
}
