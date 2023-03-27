using Core.Application.Commands;

namespace Application.Features.Generate.Commands.Query;

public class GeneratedQueryResponse : BaseStreamCommandResponse
{
    public ICollection<string>? NewFilePathsResult { get; set; }
    public ICollection<string>? UpdatedFilePathsResult { get; set; }
}
