using System;

namespace HRApp.Core.Entities
{
    public class EmployeeSkill
    {
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        public Guid SkillId { get; set; }
        public Skill Skill { get; set; } = null!;
        public string Level { get; set; } = "Beginner";  // Beginner, Intermediate, Expert
    }
}