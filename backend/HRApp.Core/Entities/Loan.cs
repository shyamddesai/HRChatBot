using System;

namespace HRApp.Core.Entities
{
    public class Loan
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        
        public string LoanType { get; set; } = string.Empty; // Car, Housing, Personal
        public decimal Amount { get; set; }
        public decimal InterestRate { get; set; } // e.g., 0.05 for 5%
        public int TenureMonths { get; set; }
        public decimal MonthlyDeduction { get; set; }
        
        public string Status { get; set; } = "Active"; // Active, PaidOff, Defaulted
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        
        // Eligibility check results
        public bool WasEligible { get; set; }
        public string? EligibilityReason { get; set; }
    }
}