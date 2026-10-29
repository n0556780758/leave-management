using LeaveManagement.Api.Models;
using LeaveManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagement.Api.Controllers;

[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveRequestService _leaveRequestService;

    public LeaveRequestsController(LeaveRequestService leaveRequestService)
    {
        _leaveRequestService = leaveRequestService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var all = await _leaveRequestService.GetAllAsync();
        return Ok(all);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string name)
    {
        var results = await _leaveRequestService.SearchAsync(name);
        return Ok(results);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeaveRequestDto dto)
    {
        var result = await _leaveRequestService.CreateAsync(dto);

        if (result.IsNotFound)
            return NotFound(result.ErrorMessage);

        if (result.IsBadRequest)
            return BadRequest(result.ErrorMessage);

        return Ok(result.Data);
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var result = await _leaveRequestService.ApproveAsync(id);

        return result switch
        {
            ApproveResult.Success => NoContent(),
            ApproveResult.NotFound => NotFound("Leave request not found"),
            ApproveResult.Conflict => Conflict("Only pending leave requests can be approved"),
            _ => StatusCode(500)
        };
    }
}