using HRApp.Core.Entities;
using HRApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HRApp.Infrastructure.Seed
{
    public static class DbInitializer
    {
        private static Random _random = new Random(42);

        public static void Initialize(AppDbContext context)
        {
            context.Database.Migrate();
            if (context.Employees.Any()) return;

            var departments = new[] { "IT", "HR", "Finance", "Sales", "Marketing", "Operations", "Engineering", "Product", "Legal", "Admin" };
            var firstNames = new[] { "John", "Jane", "Michael", "Sarah", "David", "Emily", "James", "Linda", "Robert", "Patricia", "William", "Jennifer", "Richard", "Elizabeth", "Thomas", "Susan", "Joseph", "Jessica", "Charles", "Karen", "Christopher", "Nancy", "Daniel", "Lisa", "Matthew", "Betty", "Anthony", "Margaret", "Mark", "Sandra" };
            var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson" };
            var skillNames = new[] { "C#", "Python", "JavaScript", "Java", "SQL", "Project Management", "HR Policies", "Recruitment", "Sales", "Marketing", "Finance", "Legal", "Operations", "Product Management", "UI/UX" };
            var leaveTypes = new[] { "Annual", "Sick", "Emergency", "Maternity", "Paternity" };
            var loanTypes = new[] { "Car", "Housing", "Personal" };

            // Skills
            var skills = skillNames.Select(name => new Skill { Id = Guid.NewGuid(), Name = name }).ToList();
            context.Skills.AddRange(skills);
            context.SaveChanges();

            // Employees – we'll collect them in a list and ensure unique emails
            var employees = new List<Employee>();
            var usedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Hardcoded HR admin
            var hrUser = new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeCode = "HR001",
                FullName = "Admin User",
                Email = "admin@hr.com",
                Role = "HR",
                Grade = "Grade 15",
                Department = "HR",
                HireDate = DateTime.UtcNow.AddYears(-2),
                Status = "Active"
            };
            employees.Add(hrUser);
            usedEmails.Add(hrUser.Email);

            // Hardcoded John Doe
            var john = new Employee
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
            employees.Add(john);
            usedEmails.Add(john.Email);

            // Hardcoded Jane Smith
            var jane = new Employee
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
            employees.Add(jane);
            usedEmails.Add(jane.Email);

            // Generate 40+ additional employees with unique emails
            for (int i = 0; i < 45; i++)
            {
                string firstName, lastName, email;
                do
                {
                    firstName = firstNames[_random.Next(firstNames.Length)];
                    lastName = lastNames[_random.Next(lastNames.Length)];
                    email = $"{firstName.ToLower()}.{lastName.ToLower()}@company.com";
                } while (usedEmails.Contains(email));

                var fullName = $"{firstName} {lastName}";
                var department = departments[_random.Next(departments.Length)];
                var gradeNum = _random.Next(1, 16);
                var grade = $"Grade {gradeNum}";
                var hireDate = DateTime.UtcNow.AddDays(-_random.Next(365 * 5));
                var status = _random.NextDouble() > 0.2 ? "Active" : "Archived";
                var terminationDate = status == "Archived" ? (DateTime?)DateTime.UtcNow.AddDays(-_random.Next(1, 180)) : null;
                var role = department == "HR" && _random.NextDouble() > 0.7 ? "HR" : "Employee";

                var emp = new Employee
                {
                    Id = Guid.NewGuid(),
                    EmployeeCode = $"EMP{(employees.Count + 1):D3}",
                    FullName = fullName,
                    Email = email,
                    Role = role,
                    Grade = grade,
                    Department = department,
                    HireDate = hireDate,
                    Status = status,
                    TerminationDate = terminationDate
                };
                employees.Add(emp);
                usedEmails.Add(email);
            }

            context.Employees.AddRange(employees);
            context.SaveChanges();

            // Reload to get computed GradeNumber
            employees = context.Employees.ToList();

            // Assign managers
            foreach (var emp in employees.Where(e => e.Role == "Employee" && _random.NextDouble() > 0.3))
            {
                var possibleManagers = employees.Where(e => e.Role == "HR" || (e.GradeNumber ?? 0) >= 12).ToList();
                if (possibleManagers.Any())
                {
                    emp.ManagerId = possibleManagers[_random.Next(possibleManagers.Count)].Id;
                }
            }
            context.SaveChanges();

            // Assign skills
            foreach (var emp in employees)
            {
                var numSkills = _random.Next(1, 4);
                var empSkills = new List<EmployeeSkill>();
                for (int i = 0; i < numSkills; i++)
                {
                    var skill = skills[_random.Next(skills.Count)];
                    if (!empSkills.Any(es => es.SkillId == skill.Id))
                    {
                        empSkills.Add(new EmployeeSkill
                        {
                            EmployeeId = emp.Id,
                            SkillId = skill.Id,
                            Level = _random.Next(3) switch { 0 => "Beginner", 1 => "Intermediate", _ => "Expert" }
                        });
                    }
                }
                context.EmployeeSkills.AddRange(empSkills);
            }
            context.SaveChanges();

            // Salaries
            var salaries = new List<Salary>();
            foreach (var emp in employees)
            {
                int gradeNum = emp.GradeNumber ?? 1;
                decimal baseSalary = gradeNum * 1000 + _random.Next(500, 2000);
                salaries.Add(new Salary
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = emp.Id,
                    BaseSalary = baseSalary,
                    Currency = "AED",
                    EffectiveFrom = emp.HireDate
                });

                if (_random.NextDouble() > 0.6)
                {
                    var newSalary = baseSalary + _random.Next(1000, 3000);
                    var effectiveTo = emp.HireDate.AddMonths(_random.Next(6, 24));
                    salaries.Last().EffectiveTo = effectiveTo;
                    salaries.Add(new Salary
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = emp.Id,
                        BaseSalary = newSalary,
                        Currency = "AED",
                        EffectiveFrom = effectiveTo
                    });
                }
            }
            context.Salaries.AddRange(salaries);
            context.SaveChanges();

            // Leave summaries
            var leaveSummaries = new List<LeaveSummary>();
            foreach (var emp in employees.Where(e => e.Status == "Active"))
            {
                var usedDays = _random.Next(0, 15);
                leaveSummaries.Add(new LeaveSummary
                {
                    EmployeeId = emp.Id,
                    Year = DateTime.UtcNow.Year,
                    AnnualEntitlement = 30,
                    UsedDays = usedDays,
                    RemainingDays = 30 - usedDays
                });
            }
            context.LeaveSummaries.AddRange(leaveSummaries);
            context.SaveChanges();

            // Leave requests
            var leaveRequests = new List<LeaveRequest>();
            foreach (var emp in employees.Where(e => e.Status == "Active"))
            {
                for (int i = 0; i < _random.Next(0, 4); i++)
                {
                    var start = DateTime.UtcNow.AddDays(_random.Next(-60, 60));
                    var end = start.AddDays(_random.Next(1, 10));
                    var type = leaveTypes[_random.Next(leaveTypes.Length)];
                    var status = _random.NextDouble() switch
                    {
                        < 0.4 => "Pending",
                        < 0.7 => "Approved",
                        _ => "Rejected"
                    };
                    var approver = status != "Pending" ? employees.FirstOrDefault(e => e.Role == "HR") : null;
                    leaveRequests.Add(new LeaveRequest
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = emp.Id,
                        StartDate = start,
                        EndDate = end,
                        Type = type,
                        Status = status,
                        Reason = $"Request for {type} leave",
                        ApprovedById = approver?.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-_random.Next(1, 30))
                    });
                }
            }
            context.LeaveRequests.AddRange(leaveRequests);
            context.SaveChanges();

            // Loans
            var loans = new List<Loan>();
            foreach (var emp in employees.Where(e => e.Status == "Active" && (e.GradeNumber ?? 0) >= 8))
            {
                if (_random.NextDouble() < 0.3)
                {
                    var loanType = loanTypes[_random.Next(loanTypes.Length)];
                    var currentSalary = emp.Salaries.FirstOrDefault(s => s.EffectiveTo == null)?.BaseSalary ?? 10000;
                    decimal amount = loanType switch
                    {
                        "Car" => Math.Min(currentSalary * 5, 100000),
                        "Housing" => Math.Min(currentSalary * 10, 500000),
                        _ => currentSalary
                    };
                    decimal interest = loanType switch
                    {
                        "Car" => 0.04m,
                        "Housing" => 0.03m,
                        _ => 0.06m
                    };
                    int tenure = loanType switch
                    {
                        "Car" => 48,
                        "Housing" => 120,
                        _ => 12
                    };
                    decimal monthlyRate = interest / 12;
                    double factor = Math.Pow((double)(1 + monthlyRate), tenure);
                    decimal monthlyDeduction = amount * monthlyRate * (decimal)factor / ((decimal)factor - 1);

                    loans.Add(new Loan
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = emp.Id,
                        LoanType = loanType,
                        Amount = amount,
                        InterestRate = interest,
                        TenureMonths = tenure,
                        MonthlyDeduction = Math.Round(monthlyDeduction, 2),
                        Status = _random.NextDouble() > 0.2 ? "Active" : "PaidOff",
                        StartDate = DateTime.UtcNow.AddMonths(-_random.Next(1, 24)),
                        WasEligible = true,
                        EligibilityReason = $"Meets criteria for {loanType} loan"
                    });
                }
            }
            context.Loans.AddRange(loans);
            context.SaveChanges();

            // Ensure Jane Smith has an active car loan
            var janeSmith = employees.FirstOrDefault(e => e.FullName == "Jane Smith");
            if (janeSmith != null && !context.Loans.Any(l => l.EmployeeId == janeSmith.Id && l.LoanType == "Car" && l.Status == "Active"))
            {
                context.Loans.Add(new Loan
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = janeSmith.Id,
                    LoanType = "Car",
                    Amount = 50000,
                    InterestRate = 0.04m,
                    TenureMonths = 48,
                    MonthlyDeduction = 1135,
                    Status = "Active",
                    StartDate = DateTime.UtcNow.AddMonths(-3),
                    WasEligible = true,
                    EligibilityReason = "Grade 11 meets minimum requirement (Grade 10+)"
                });
                context.SaveChanges();
            }
        }
    }
}