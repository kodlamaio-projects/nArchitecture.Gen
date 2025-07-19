using Core.Application.Commands;

namespace NArchitecture.Gen.Application.Features.ProjectManagement.Commands.New;

public class CreatedNewProjectResponse : BaseStreamCommandResponse
{
    public ICollection<string>? NewFilePathsResult { get; set; }
}
