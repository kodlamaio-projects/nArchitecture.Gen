using Application.Features.Generate.Commands;
using Core.Application.Commands;

namespace Application.Features.Ai.Commands.Init;

public class InitializedAiResponse : BaseStreamCommandResponse, IGeneratedFilesResponse
{
    public ICollection<string>? NewFilePathsResult { get; set; }
    public ICollection<string>? UpdatedFilePathsResult { get; set; }
}
