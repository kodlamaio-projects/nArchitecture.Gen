using Core.Application.Commands;

namespace Application.Features.Create.Commands.New;

public class CreatedNewProjectResponse : BaseStreamCommandResponse
{
    public ICollection<string>? NewFilePathsResult { get; set; }
}
