using System.Text.Json.Serialization;
using LeaveManagement.Api.Data;
using LeaveManagement.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL by default — `docker compose up` brings one up next to the API.
// No Docker on the machine? Set Database:Provider=Sqlite and the app runs
// against a local file instead. See the README.
var useSqlite = string.Equals(
    builder.Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<LeaveDbContext>(options =>
{
    if (useSqlite)
        options.UseSqlite(builder.Configuration.GetConnectionString("LeaveSqlite"));
    else
        options.UseNpgsql(builder.Configuration.GetConnectionString("Leave"));
});

// POC returns EF entities straight from the controller, which have circular
// navigations (LeaveRequest <-> Employee). Tolerate the cycles so the app runs;
// returning DTOs instead is a fair improvement for a candidate to suggest.
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<LeaveRequestService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeaveDbContext>();
    db.Database.EnsureCreated();
    LeaveDbContext.Seed(db);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAngular");
app.MapControllers();

app.Run();

// Exposed so the integration tests can reference the entry point.
public partial class Program { }
