using Microsoft.EntityFrameworkCore;
using HRApp.Core.Entities;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace HRApp.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Salary> Salaries { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<EmployeeSkill> EmployeeSkills { get; set; }
        public DbSet<LeaveSummary> LeaveSummaries { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Loan> Loans { get; set; }


        // Enable vector support for pgvector
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(o => o.UseVector());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Employee self-reference (manager)
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Manager)
                .WithMany() // No navigation property on Employee for subordinates
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            // LeaveRequest relationships
            // Employee -> LeaveRequests (requestor)
            modelBuilder.Entity<LeaveRequest>()
                .HasOne(l => l.Employee)
                .WithMany(e => e.LeaveRequests)
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade); // Or Restrict based on your policy

            // ApprovedBy (approver) relationship
            modelBuilder.Entity<LeaveRequest>()
                .HasOne(l => l.ApprovedBy)
                .WithMany() // No collection for approved leaves on Employee
                .HasForeignKey(l => l.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LeaveSummary>()
                .HasOne(ls => ls.Employee)
                .WithMany()
                .HasForeignKey(ls => ls.EmployeeId);

            // Composite key for EmployeeSkill
            modelBuilder.Entity<EmployeeSkill>()
                .HasKey(es => new { es.EmployeeId, es.SkillId });

            // Indexes for uniqueness
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.EmployeeCode)
                .IsUnique();

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.Email)
                .IsUnique();

            modelBuilder.Entity<Skill>()
                .HasIndex(s => s.Name)
                .IsUnique();

            // Salary: effective period
            modelBuilder.Entity<Salary>()
                .Property(s => s.EffectiveTo)
                .IsRequired(false);

            modelBuilder.Entity<LeaveSummary>()
                .HasKey(ls => new { ls.EmployeeId, ls.Year });

            modelBuilder.Entity<Employee>()
                .Property(e => e.GradeNumber)
                .HasComputedColumnSql(@"CAST(SUBSTRING(""Grade"" FROM '\d+') AS INTEGER)", stored: true);

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.GradeNumber);

            // For Documents vector support
            modelBuilder.Entity<Document>()
                .Property(d => d.Embedding)
                .HasColumnType("vector(768)");
            
            modelBuilder.Entity<Loan>()
                .HasOne(l => l.Employee)
                .WithMany()
                .HasForeignKey(l => l.EmployeeId);
        }
    }
}