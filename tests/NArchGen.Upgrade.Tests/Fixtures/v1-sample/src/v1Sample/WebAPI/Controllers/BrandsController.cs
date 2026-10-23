using Application.Features.Brands.Commands.Create;
using Application.Features.Brands.Commands.Delete;
using Application.Features.Brands.Commands.Update;
using Application.Features.Brands.Queries.GetById;
using Application.Features.Brands.Queries.GetList;
using NArchitecture.Core.Application.Requests;
using NArchitecture.Core.Application.Responses;
using Microsoft.AspNetCore.Mvc;
using Application.Features.Brands.Commands.SyncBrand;
using Application.Features.Brands.Queries.GetBrandByName;

namespace WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BrandsController : BaseController
{
    [HttpPost]
    public async Task<ActionResult<CreatedBrandResponse>> Add([FromBody] CreateBrandCommand command)
    {
        CreatedBrandResponse response = await Mediator.Send(command);

        return CreatedAtAction(nameof(GetById), new { response.Id }, response);
    }

    [HttpPut]
    public async Task<ActionResult<UpdatedBrandResponse>> Update([FromBody] UpdateBrandCommand command)
    {
        UpdatedBrandResponse response = await Mediator.Send(command);

        return Ok(response);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<DeletedBrandResponse>> Delete([FromRoute] Guid id)
    {
        DeleteBrandCommand command = new() { Id = id };

        DeletedBrandResponse response = await Mediator.Send(command);

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GetByIdBrandResponse>> GetById([FromRoute] Guid id)
    {
        GetByIdBrandQuery query = new() { Id = id };

        GetByIdBrandResponse response = await Mediator.Send(query);

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<GetListResponse<GetListBrandListItemDto>>> GetList([FromQuery] PageRequest pageRequest)
    {
        GetListBrandQuery query = new() { PageRequest = pageRequest };

        GetListResponse<GetListBrandListItemDto> response = await Mediator.Send(query);

        return Ok(response);
    }
    
    [HttpPost("SyncBrand")]
    public async Task<ActionResult<SyncBrandResponse>> SyncBrand([FromBody] SyncBrandCommand syncBrandCommand)
    {
        SyncBrandResponse response = await Mediator.Send(syncBrandCommand);
        return Ok(response);
    }
    
    [HttpGet("GetBrandByName")]
    public async Task<ActionResult<GetBrandByNameResponse>> GetBrandByName([FromQuery] GetBrandByNameQuery getBrandByNameQuery)
    {
        GetBrandByNameResponse response = await Mediator.Send(getBrandByNameQuery);
        return Ok(response);
    }
}
