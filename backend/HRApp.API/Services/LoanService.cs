using HRApp.Core.Entities;
using HRApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRApp.API.Services
{
    public interface ILoanService
    {
        Task<LoanEligibilityResult> CheckEligibilityAsync(Guid employeeId, string loanType);
        Task<Loan?> GetActiveLoanAsync(Guid employeeId, string loanType);
    }

    public class LoanEligibilityResult
    {
        public bool IsEligible { get; set; }
        public string Reason { get; set; } = string.Empty;
        public decimal? MaxAmount { get; set; }
        public decimal? SuggestedMonthlyDeduction { get; set; }
        public int? SuggestedTenure { get; set; }
        public List<string> RequirementsMet { get; set; } = new();
        public List<string> RequirementsMissing { get; set; } = new();
    }

    public class LoanService : ILoanService
    {
        private readonly AppDbContext _context;

        public LoanService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<LoanEligibilityResult> CheckEligibilityAsync(Guid employeeId, string loanType)
        {
            var employee = await _context.Employees
                .Include(e => e.Salaries)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
                return new LoanEligibilityResult 
                { 
                    IsEligible = false, 
                    Reason = "Employee not found" 
                };

            var currentSalary = employee.Salaries
                .Where(s => s.EffectiveTo == null)
                .OrderByDescending(s => s.EffectiveFrom)
                .Select(s => s.BaseSalary)
                .FirstOrDefault();

            var gradeNum = employee.GradeNumber ?? 0;
            
            var result = new LoanEligibilityResult();
            
            // Business rules
            switch (loanType.ToLower())
            {
                case "car":
                    await EvaluateCarLoan(employee, gradeNum, currentSalary, result);
                    break;
                case "housing":
                    await EvaluateHousingLoan(employee, gradeNum, currentSalary, result);
                    break;
                case "personal":
                    await EvaluatePersonalLoan(employee, gradeNum, currentSalary, result);
                    break;
                default:
                    result.IsEligible = false;
                    result.Reason = $"Unknown loan type: {loanType}. Supported: Car, Housing, Personal";
                    break;
            }

            return result;
        }

        private async Task EvaluateCarLoan(Employee emp, int grade, decimal salary, LoanEligibilityResult result)
        {
            // Rule: Grade 10+ and salary >= 8000
            var hasExistingCarLoan = await _context.Loans
                .AnyAsync(l => l.EmployeeId == emp.Id && l.LoanType == "Car" && l.Status == "Active");

            if (grade >= 10) result.RequirementsMet.Add($"Grade {grade} meets minimum (Grade 10)");
            else result.RequirementsMissing.Add($"Grade {grade} below minimum (Grade 10)");

            if (salary >= 8000) result.RequirementsMet.Add($"Salary AED {salary:N0} meets minimum (AED 8,000)");
            else result.RequirementsMissing.Add($"Salary AED {salary:N0} below minimum (AED 8,000)");

            if (!hasExistingCarLoan) result.RequirementsMet.Add("No existing active car loan");
            else result.RequirementsMissing.Add("Already has active car loan");

            result.IsEligible = grade >= 10 && salary >= 8000 && !hasExistingCarLoan;

            if (result.IsEligible)
            {
                var maxAmount = Math.Min(salary * 5, 100000); // Max 5x salary or 100k
                result.MaxAmount = maxAmount;
                result.SuggestedTenure = 48;
                result.SuggestedMonthlyDeduction = CalculateEMI(maxAmount, 0.04m, 48);
                result.Reason = $"Eligible for car loan up to AED {maxAmount:N0} based on grade and salary";
            }
            else
            {
                result.Reason = "Not eligible for car loan: " + string.Join(", ", result.RequirementsMissing);
            }
        }

        private async Task EvaluateHousingLoan(Employee emp, int grade, decimal salary, LoanEligibilityResult result)
        {
            // Rule: Grade 12+ and salary >= 15000 and 2+ years tenure
            var tenureYears = (DateTime.UtcNow - emp.HireDate).TotalDays / 365.25;
            var hasExistingHousingLoan = await _context.Loans
                .AnyAsync(l => l.EmployeeId == emp.Id && l.LoanType == "Housing" && l.Status == "Active");

            if (grade >= 12) result.RequirementsMet.Add($"Grade {grade} meets minimum (Grade 12)");
            else result.RequirementsMissing.Add($"Grade {grade} below minimum (Grade 12)");

            if (salary >= 15000) result.RequirementsMet.Add($"Salary AED {salary:N0} meets minimum (AED 15,000)");
            else result.RequirementsMissing.Add($"Salary AED {salary:N0} below minimum (AED 15,000)");

            if (tenureYears >= 2) result.RequirementsMet.Add($"Tenure {tenureYears:F1} years meets minimum (2 years)");
            else result.RequirementsMissing.Add($"Tenure {tenureYears:F1} years below minimum (2 years)");

            if (!hasExistingHousingLoan) result.RequirementsMet.Add("No existing active housing loan");
            else result.RequirementsMissing.Add("Already has active housing loan");

            result.IsEligible = grade >= 12 && salary >= 15000 && tenureYears >= 2 && !hasExistingHousingLoan;

            if (result.IsEligible)
            {
                var maxAmount = Math.Min(salary * 10, 500000); // Max 10x salary or 500k
                result.MaxAmount = maxAmount;
                result.SuggestedTenure = 120; // 10 years
                result.SuggestedMonthlyDeduction = CalculateEMI(maxAmount, 0.03m, 120);
                result.Reason = $"Eligible for housing loan up to AED {maxAmount:N0}";
            }
            else
            {
                result.Reason = "Not eligible for housing loan: " + string.Join(", ", result.RequirementsMissing);
            }
        }

        private async Task EvaluatePersonalLoan(Employee emp, int grade, decimal salary, LoanEligibilityResult result)
        {
            // Rule: Any active employee, max 1x salary
            if (emp.Status == "Active") result.RequirementsMet.Add("Active employee");
            else result.RequirementsMissing.Add("Employee not active");

            result.IsEligible = emp.Status == "Active";

            if (result.IsEligible)
            {
                var maxAmount = salary; // Max 1x salary
                result.MaxAmount = maxAmount;
                result.SuggestedTenure = 12;
                result.SuggestedMonthlyDeduction = CalculateEMI(maxAmount, 0.06m, 12);
                result.Reason = $"Eligible for personal loan up to AED {maxAmount:N0} (1x salary)";
            }
            else
            {
                result.Reason = "Not eligible for personal loan";
            }
        }

        private decimal CalculateEMI(decimal principal, decimal annualRate, int months)
        {
            var monthlyRate = annualRate / 12;
            var factor = (decimal)Math.Pow((double)(1 + monthlyRate), months);
            return principal * monthlyRate * factor / (factor - 1);
        }

        public async Task<Loan?> GetActiveLoanAsync(Guid employeeId, string loanType)
        {
            return await _context.Loans
                .FirstOrDefaultAsync(l => 
                    l.EmployeeId == employeeId && 
                    l.LoanType == loanType && 
                    l.Status == "Active");
        }
    }
}