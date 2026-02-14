using System;

namespace HRApp.Core.Entities
{
    public class LeaveRequest
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Type { get; set; } = "Annual";
        public string Status { get; set; } = "Pending";
        public string? Reason { get; set; }
        public Guid? ApprovedById { get; set; }
        public Employee? ApprovedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}