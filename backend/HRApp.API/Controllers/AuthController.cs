using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using HRApp.Core.Entities;
using HRApp.Infrastructure.Data;

namespace HRApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // In a real app, hash passwords. For demo, we'll just check plain text (since we seeded plain email as password)
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email == request.Email && e.Status == "Active");

            if (employee == null)
                return Unauthorized("Invalid email or password");

            // For demo: password is email (e.g., admin@hr.com)
            if (request.Password != employee.Email) // This is just a placeholder! In production, use hashed passwords.
                return Unauthorized("Invalid email or password");

            // Generate token
            var token = GenerateJwtToken(employee);
            return Ok(new { token, role = employee.Role, employeeId = employee.Id, fullName = employee.FullName });
        }

        private string GenerateJwtToken(Employee employee)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured"));
            var expireMinutes = double.Parse(_configuration["Jwt:ExpireMinutes"] ?? "120");
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, employee.Id.ToString()),
                    new Claim(ClaimTypes.Email, employee.Email),
                    new Claim(ClaimTypes.Role, employee.Role),
                    new Claim("EmployeeCode", employee.EmployeeCode)
                }),
                Expires = DateTime.UtcNow.AddMinutes(expireMinutes),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}