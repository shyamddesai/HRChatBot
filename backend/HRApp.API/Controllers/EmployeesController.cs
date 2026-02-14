using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRApp.Core.Entities;
using HRApp.Infrastructure.Data;
using System.Security.Claims;

namespace HRApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmployeesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EmployeesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "HR")] // Only HR can list all employees
        public async Task<ActionResult<IEnumerable<Employee>>> GetEmployees()
        {
            return await _context.Employees.ToListAsync();
        }

        [HttpGet("me")]
        public async Task<ActionResult<Employee>> GetMyProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var employee = await _context.Employees
                .Include(e => e.Salaries)
                .Include(e => e.LeaveRequests)
                .FirstOrDefaultAsync(e => e.Id.ToString() == userId);
            if (employee == null)
                return NotFound();
            return employee;
        }
    }
}