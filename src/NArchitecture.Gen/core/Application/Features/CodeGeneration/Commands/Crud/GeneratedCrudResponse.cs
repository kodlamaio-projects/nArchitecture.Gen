using Core.Application.Commands;

namespace NArchitecture.Gen.Application.Features.CodeGeneration.Commands.Crud;

public class GeneratedCrudResponse : BaseStreamCommandResponse
{
    public ICollection<string>? NewFilePathsResult { get; set; }
    public ICollection<string>? UpdatedFilePathsResult { get; set; }
}
