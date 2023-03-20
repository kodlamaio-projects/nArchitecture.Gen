using Core.Application.Commands;

namespace Application.Features.Generate.Commands.Crud;

public class GeneratedCrudResponse : BaseStreamCommandResponse
{
    public ICollection<string>? NewFilePathsResult { get; set; }
    public ICollection<string>? UpdatedFilePathsResult { get; set; }
}
