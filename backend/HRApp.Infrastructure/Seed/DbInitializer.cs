using HRApp.Core.Entities;
using HRApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace HRApp.Infrastructure.Seed
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // Ensure database is created
            context.Database.Migrate();

            // Look for any employees
            if (context.Employees.Any())
            {
                return;   // DB has been seeded
            }

            // Seed Skills
            var skills = new Skill[]
            {
                new Skill { Id = Guid.NewGuid(), Name = "C#" },
                new Skill { Id = Guid.NewGuid(), Name = "Python" },
                new Skill { Id = Guid.NewGuid(), Name = "JavaScript" },
                new Skill { Id = Guid.NewGuid(), Name = "Project Management" },
                new Skill { Id = Guid.NewGuid(), Name = "HR Policies" },
                new Skill { Id = Guid.NewGuid(), Name = "Recruitment" }
            };
            context.Skills.AddRange(skills);
            context.SaveChanges();

            // Seed Employees (including an HR user)
            var hrUser = new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeCode = "HR001",
                FullName = "Admin User",
                Email = "admin@hr.com",
                Role = "HR",
                Grade = "Grade 15",
                Department = "Human Resources",
                HireDate = DateTime.UtcNow.AddYears(-2),
                Status = "Active"
            };

            var emp1 = new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeCode = "EMP001",
                FullName = "John Doe",
                Email = "john.doe@company.com",
                Role = "Employee",
                Grade = "Grade 10",
                Department = "IT",
                HireDate = DateTime.UtcNow.AddYears(-1),
                Status = "Active"
            };

            var emp2 = new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeCode = "EMP002",
                FullName = "Jane Smith",
                Email = "jane.smith@company.com",
                Role = "Employee",
                Grade = "Grade 11",
                Department = "HR",
                HireDate = DateTime.UtcNow.AddMonths(-6),
                Status = "Active"
            };

            context.Employees.AddRange(hrUser, emp1, emp2);
            context.SaveChanges();

            // Assign Skills to Employees
            context.EmployeeSkills.AddRange(
                new EmployeeSkill { EmployeeId = emp1.Id, SkillId = skills[0].Id, Level = "Expert" }, // C#
                new EmployeeSkill { EmployeeId = emp1.Id, SkillId = skills[1].Id, Level = "Intermediate" }, // Python
                new EmployeeSkill { EmployeeId = emp2.Id, SkillId = skills[3].Id, Level = "Expert" }, // Project Management
                new EmployeeSkill { EmployeeId = emp2.Id, SkillId = skills[4].Id, Level = "Intermediate" } // HR Policies
            );
            context.SaveChanges();

            // Seed Salaries
            context.Salaries.AddRange(
                new Salary { EmployeeId = emp1.Id, BaseSalary = 10000, Currency = "AED", EffectiveFrom = emp1.HireDate },
                new Salary { EmployeeId = emp2.Id, BaseSalary = 12000, Currency = "AED", EffectiveFrom = emp2.HireDate }
            );
            context.SaveChanges();

            // Seed Leave Requests (optional)
            context.LeaveRequests.Add(new LeaveRequest
            {
                EmployeeId = emp1.Id,
                StartDate = DateTime.UtcNow.AddDays(10),
                EndDate = DateTime.UtcNow.AddDays(15),
                Type = "Annual",
                Status = "Pending",
                Reason = "Family vacation",
                CreatedAt = DateTime.UtcNow
            });
            context.SaveChanges();
        }
    }
}