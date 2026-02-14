using System;
using System.Collections.Generic;

namespace HRApp.Core.Entities
{
    public class Employee
    {
        public Guid Id { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Employee";  // "HR" or "Employee"
        public string Grade { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public Guid? ManagerId { get; set; }
        public Employee? Manager { get; set; }
        public string Status { get; set; } = "Active";  // Active, Archived
        public DateTime HireDate { get; set; }
        public DateTime? TerminationDate { get; set; }

        // Navigation properties
        public ICollection<Salary> Salaries { get; set; } = new List<Salary>();
        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
        public ICollection<EmployeeSkill> EmployeeSkills { get; set; } = new List<EmployeeSkill>();
    }
}