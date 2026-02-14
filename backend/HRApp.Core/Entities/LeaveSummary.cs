using System;
using System.Collections.Generic;

namespace HRApp.Core.Entities
{
    public class LeaveSummary
    {
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        public int Year { get; set; }
        public int AnnualEntitlement { get; set; }
        public int UsedDays { get; set; }
        public int RemainingDays { get; set; }
    }
}