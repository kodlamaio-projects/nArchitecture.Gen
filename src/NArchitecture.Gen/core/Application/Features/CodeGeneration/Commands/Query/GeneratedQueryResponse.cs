using Core.Application.Commands;

namespace NArchitecture.Gen.Application.Features.CodeGeneration.Commands.Query;

public class GeneratedQueryResponse : BaseStreamCommandResponse
{
    public ICollection<string>? NewFilePathsResult { get; set; }
    public ICollection<string>? UpdatedFilePathsResult { get; set; }
}
