using LeaveManagement.Api.Controllers;
using LeaveManagement.Api.Models;
using LeaveManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LeaveManagement.Tests;

public class LeaveRequestsTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _database;

    public LeaveRequestsTests(TestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task Create_WithinQuota_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var service = new LeaveRequestService(db);
        var controller = new LeaveRequestsController(service);

        // Act: request 3 days, well within the quota.
        var result = await controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Single(db.LeaveRequests);
    }

    [Fact]
    public async Task Create_ExceedingRemainingVacationBalance_ReturnsBadRequest()
    {
        using var db = _database.NewDb();

        var emp = new Employee
        {
            Name = "Test Emp",
            AnnualQuota = 20
        };

        db.Employees.Add(emp);
        db.SaveChanges();

        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 18),
            Days = 18,
            Status = LeaveStatus.Approved
        });

        db.SaveChanges();

        var service = new LeaveRequestService(db);
        var controller = new LeaveRequestsController(service);

        var result = await controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Single(db.LeaveRequests);
    }

    [Fact]
    public async Task Approve_PendingRequest_ApprovesRequest()
    {
        using var db = _database.NewDb();

        var employee = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(employee);
        db.SaveChanges();

        var request = new LeaveRequest
        {
            EmployeeId = employee.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3),
            Days = 3,
            Status = LeaveStatus.Pending
        };

        db.LeaveRequests.Add(request);
        db.SaveChanges();

        var service = new LeaveRequestService(db);
        var controller = new LeaveRequestsController(service);
        var result = await controller.Approve(request.Id);

        Assert.IsType<NoContentResult>(result);
        var status = await db.LeaveRequests.Where(r => r.Id == request.Id).Select(r => r.Status).SingleAsync();
        Assert.Equal(LeaveStatus.Approved, status);
    }

    [Fact]
    public async Task Approve_NonExistingRequest_ReturnsNotFound()
    {
        using var db = _database.NewDb();
        var service = new LeaveRequestService(db);
        var controller = new LeaveRequestsController(service);
        var result = await controller.Approve(999);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData(LeaveStatus.Approved)]
    [InlineData(LeaveStatus.Rejected)]
    public async Task Approve_NonPendingRequest_ReturnsConflict(LeaveStatus status)
    {
        using var db = _database.NewDb();

        var employee = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(employee);
        db.SaveChanges();

        var request = new LeaveRequest
        {
            EmployeeId = employee.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3),
            Days = 3,
            Status = status
        };

        db.LeaveRequests.Add(request);
        db.SaveChanges();

        var service = new LeaveRequestService(db);
        var controller = new LeaveRequestsController(service);
        var result = await controller.Approve(request.Id);
        Assert.IsType<ConflictObjectResult>(result);
        var actualStatus = await db.LeaveRequests.Where(r => r.Id == request.Id).Select(r => r.Status).SingleAsync();
        Assert.Equal(status, actualStatus);
    }
}