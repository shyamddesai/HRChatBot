using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRApp.Core.Entities;
using HRApp.Infrastructure.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace HRApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmployeesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private static readonly SemaphoreSlim _codeGenerationLock = new SemaphoreSlim(1, 1);

        public EmployeesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "HR")]
        public async Task<ActionResult<IEnumerable<object>>> GetEmployees()
        {
            var employees = await _context.Employees
                .Where(e => e.Status == "Active")
                .OrderBy(e => e.FullName)
                .Select(e => new {
                    e.Id,
                    e.EmployeeCode,
                    e.FullName,
                    e.Email,
                    e.Department,
                    e.Grade,
                    e.Status,
                    e.HireDate
                })
                .ToListAsync();

            return Ok(employees);
        }

        [HttpGet("all")]
        [Authorize(Roles = "HR")]
        public async Task<ActionResult<IEnumerable<object>>> GetAllEmployeesIncludingArchived()
        {
            var employees = await _context.Employees
                .OrderByDescending(e => e.Status == "Active")
                .ThenBy(e => e.FullName)
                .Select(e => new {
                    e.Id,
                    e.EmployeeCode,
                    e.FullName,
                    e.Email,
                    e.Department,
                    e.Grade,
                    e.Status,
                    e.HireDate
                })
                .ToListAsync();

            return Ok(employees);
        }

        [HttpGet("me")]
        public async Task<ActionResult<object>> GetMyProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id.ToString() == userId);

            if (employee == null)
                return NotFound();

            // Get salaries separately to avoid cycles
            var salaries = await _context.Salaries
                .Where(s => s.EmployeeId.ToString() == userId)
                .OrderByDescending(s => s.EffectiveFrom)
                .Select(s => new {
                    s.Id,
                    s.BaseSalary,
                    s.Currency,
                    s.EffectiveFrom,
                    s.EffectiveTo
                })
                .ToListAsync();

            // Get leave requests separately
            var leaveRequests = await _context.LeaveRequests
                .Where(l => l.EmployeeId.ToString() == userId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new {
                    l.Id,
                    l.StartDate,
                    l.EndDate,
                    l.Type,
                    l.Status,
                    l.Reason
                })
                .ToListAsync();

            return Ok(new {
                employee.Id,
                employee.EmployeeCode,
                employee.FullName,
                employee.Email,
                employee.Department,
                employee.Grade,
                employee.Role,
                employee.Status,
                employee.HireDate,
                Salaries = salaries,
                LeaveRequests = leaveRequests
            });
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "HR")]
        public async Task<ActionResult<object>> GetEmployee(Guid id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            var salaries = await _context.Salaries
                .Where(s => s.EmployeeId == id)
                .OrderByDescending(s => s.EffectiveFrom)
                .Select(s => new {
                    s.Id,
                    s.BaseSalary,
                    s.Currency,
                    s.EffectiveFrom,
                    s.EffectiveTo
                })
                .ToListAsync();

            return Ok(new {
                employee.Id,
                employee.EmployeeCode,
                employee.FullName,
                employee.Email,
                employee.Department,
                employee.Grade,
                employee.Role,
                employee.Status,
                employee.HireDate,
                Salaries = salaries
            });
        }

        public class CreateEmployeeRequest
        {
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Department { get; set; } = string.Empty;
            public string Grade { get; set; } = string.Empty;
            public string Role { get; set; } = "Employee";
            public decimal BaseSalary { get; set; }
        }

        [HttpPost]
        [Authorize(Roles = "HR")]
        public async Task<ActionResult<Employee>> CreateEmployee([FromBody] CreateEmployeeRequest request)
        {
            // Check for duplicate email
            if (await _context.Employees.AnyAsync(e => e.Email == request.Email))
                return BadRequest(new { message = "Employee with this email already exists" });

            // Use semaphore to prevent concurrent code generation conflicts
            await _codeGenerationLock.WaitAsync();
            try
            {
                var employeeCode = await GenerateEmployeeCodeAsync();

                var employee = new Employee
                {
                    Id = Guid.NewGuid(),
                    EmployeeCode = employeeCode,
                    FullName = request.FullName,
                    Email = request.Email,
                    Department = request.Department,
                    Grade = request.Grade,
                    Role = request.Role,
                    Status = "Active",
                    HireDate = DateTime.UtcNow
                };

                _context.Employees.Add(employee);

                // Create initial salary record
                if (request.BaseSalary > 0)
                {
                    var salary = new Salary
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = employee.Id,
                        BaseSalary = request.BaseSalary,
                        Currency = "AED",
                        EffectiveFrom = DateTime.UtcNow
                    };
                    _context.Salaries.Add(salary);
                }

                // Create leave summary for current year
                var leaveSummary = new LeaveSummary
                {
                    EmployeeId = employee.Id,
                    Year = DateTime.UtcNow.Year,
                    AnnualEntitlement = 30,
                    UsedDays = 0,
                    RemainingDays = 30
                };
                _context.LeaveSummaries.Add(leaveSummary);

                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, new {
                    employee.Id,
                    employee.EmployeeCode,
                    employee.FullName,
                    employee.Email,
                    employee.Department,
                    employee.Grade,
                    employee.Role,
                    employee.Status,
                    employee.HireDate
                });
            }
            finally
            {
                _codeGenerationLock.Release();
            }
        }

        [HttpPut("{id}/promote")]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> PromoteEmployee(Guid id, [FromBody] PromoteRequest request)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound();

            var oldGrade = employee.Grade;
            employee.Grade = request.NewGrade;

            // If salary increase provided, create new salary record
            if (request.NewSalary > 0)
            {
                // Close current salary
                var currentSalary = await _context.Salaries
                    .FirstOrDefaultAsync(s => s.EmployeeId == id && s.EffectiveTo == null);
                if (currentSalary != null)
                {
                    currentSalary.EffectiveTo = DateTime.UtcNow;
                }

                // Create new salary record
                var newSalary = new Salary
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = id,
                    BaseSalary = request.NewSalary,
                    Currency = "AED",
                    EffectiveFrom = DateTime.UtcNow
                };
                _context.Salaries.Add(newSalary);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Employee promoted from {oldGrade} to {request.NewGrade}", employee });
        }

        public class PromoteRequest
        {
            public string NewGrade { get; set; } = string.Empty;
            public decimal NewSalary { get; set; }
        }

        [HttpPost("{id}/archive")]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ArchiveEmployee(Guid id, [FromBody] ArchiveRequest request)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound();

            employee.Status = "Archived";
            employee.TerminationDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Employee archived successfully", employee });
        }

        [HttpPost("{id}/restore")]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> RestoreEmployee(Guid id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound();

            employee.Status = "Active";
            employee.TerminationDate = null;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Employee restored successfully", employee });
        }

        public class ArchiveRequest
        {
            public string? Reason { get; set; }
        }

        private async Task<string> GenerateEmployeeCodeAsync()
        {
            // Get all existing employee codes that match EMP### pattern
            var existingCodes = await _context.Employees
                .Where(e => e.EmployeeCode.StartsWith("EMP"))
                .Select(e => e.EmployeeCode)
                .ToListAsync();

            int maxNumber = 0;

            foreach (var code in existingCodes)
            {
                // Extract number from EMP### format using regex
                var match = Regex.Match(code, @"EMP(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                {
                    if (num > maxNumber)
                        maxNumber = num;
                }
            }

            // Generate next code
            int nextNumber = maxNumber + 1;
            return $"EMP{nextNumber:D3}";
        }
    }
}