using Core.Application.Commands;

namespace Application.Features.Generate.Commands.DynamicQuery;

public class GeneratedDynamicQueryResponse : BaseStreamCommandResponse
{
    public ICollection<string>? NewFilePathsResult { get; set; }
    public ICollection<string>? UpdatedFilePathsResult { get; set; }
}
