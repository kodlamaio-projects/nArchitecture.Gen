using Core.Application.Commands;

namespace NArchitecture.Gen.Application.Features.CodeGeneration.Commands.Command;

public class GeneratedCommandResponse : BaseStreamCommandResponse
{
    public ICollection<string>? NewFilePathsResult { get; set; }
    public ICollection<string>? UpdatedFilePathsResult { get; set; }
}
