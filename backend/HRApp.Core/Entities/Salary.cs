using System;

namespace HRApp.Core.Entities
{
    public class Salary
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        public decimal BaseSalary { get; set; }
        public string Currency { get; set; } = "AED";
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }
}