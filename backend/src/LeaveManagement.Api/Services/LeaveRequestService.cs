using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Services;

public class LeaveRequestService
{
    private readonly LeaveDbContext _db;

    public LeaveRequestService(LeaveDbContext db)
    {
        _db = db;
    }

    public async Task<List<LeaveRequest>> GetAllAsync()
    {
        return await _db.LeaveRequests
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<List<LeaveRequest>> SearchAsync(string name)
    {
        var query = _db.LeaveRequests.Include(r => r.Employee).AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(r => r.Employee != null && r.Employee.Name.Contains(name));
        }

        return await query.OrderByDescending(r => r.StartDate).ToListAsync();
    }

    public async Task<ServiceResult<LeaveRequest>> CreateAsync(CreateLeaveRequestDto dto)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == dto.EmployeeId);
        if (employee == null)
        {
            return ServiceResult<LeaveRequest>.NotFound("Employee not found");
        }

        var days = (dto.EndDate - dto.StartDate).Days + 1;

        var used = await _db.LeaveRequests
            .Where(r => r.EmployeeId == dto.EmployeeId
                        && r.Type == LeaveType.Vacation
                        && r.Status == LeaveStatus.Approved)
            .SumAsync(r => r.Days);

        if (dto.Type == LeaveType.Vacation && used + days > employee.AnnualQuota)
        {
            return ServiceResult<LeaveRequest>.BadRequest("Not enough vacation balance");
        }

        var request = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Days = days,
            Status = LeaveStatus.Pending
        };

        _db.LeaveRequests.Add(request);
        await _db.SaveChangesAsync();

        return ServiceResult<LeaveRequest>.Success(request);
    }

    public async Task<ApproveResult> ApproveAsync(int id)
    {
        var updated = await _db.LeaveRequests
            .Where(r => r.Id == id && r.Status == LeaveStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.Status, LeaveStatus.Approved));

        if (updated == 1)
        {
            return ApproveResult.Success;
        }

        var exists = await _db.LeaveRequests.AnyAsync(r => r.Id == id);
        if (!exists)
        {
            return ApproveResult.NotFound;
        }

        return ApproveResult.Conflict;
    }
}

public class ServiceResult<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsNotFound { get; private set; }
    public bool IsBadRequest { get; private set; }

    public static ServiceResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static ServiceResult<T> NotFound(string message) => new() { IsNotFound = true, ErrorMessage = message };
    public static ServiceResult<T> BadRequest(string message) => new() { IsBadRequest = true, ErrorMessage = message };
}

public enum ApproveResult
{
    Success,
    NotFound,
    Conflict
}