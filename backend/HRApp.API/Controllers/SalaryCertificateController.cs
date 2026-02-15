using HRApp.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HRApp.Infrastructure.Data;

namespace HRApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "HR")]
    public class SalaryCertificateController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalaryCertificateController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("{employeeId}")]
        public async Task<IActionResult> GenerateCertificate(Guid employeeId)
        {
            var employee = await _context.Employees
                .Include(e => e.Salaries)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
                return NotFound();

            var currentSalary = employee.Salaries
                .Where(s => s.EffectiveTo == null)
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefault();

            if (currentSalary == null)
                return BadRequest("No current salary record found.");

            var document = GeneratePdf(employee, currentSalary);
            var pdfBytes = document.GeneratePdf();

            return File(pdfBytes, "application/pdf", $"Salary_Certificate_{employee.EmployeeCode}.pdf");
        }

        private string NumberToWords(long number)
        {
            if (number == 0) return "Zero";

            string[] unitsMap = { "", "One", "Two", "Three", "Four", "Five",
                "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve",
                "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen",
                "Eighteen", "Nineteen" };

            string[] tensMap = { "", "", "Twenty", "Thirty", "Forty",
                "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            if (number < 20)
                return unitsMap[number];

            if (number < 100)
                return tensMap[number / 10] + 
                    ((number % 10 > 0) ? " " + unitsMap[number % 10] : "");

            if (number < 1000)
                return unitsMap[number / 100] + " Hundred" +
                    ((number % 100 > 0) ? " and " + NumberToWords(number % 100) : "");

            if (number < 1000000)
                return NumberToWords(number / 1000) + " Thousand" +
                    ((number % 1000 > 0) ? " " + NumberToWords(number % 1000) : "");

            return number.ToString();
        }

        private IDocument GeneratePdf(Employee employee, Salary salary)
        {
            return QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);

                    page.DefaultTextStyle(x =>
                        x.FontSize(11)
                        .FontFamily("Times New Roman")
                        .LineHeight(1.5f));

                    // ================= HEADER =================
                    page.Header().Column(header =>
                    {
                        header.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("DESAI GLOBAL INDUSTRIES")
                                    .Bold()
                                    .FontSize(18);

                                col.Item().Text("PeopleCore – Human Capital Division")
                                    .FontSize(12)
                                    .FontColor(Colors.Grey.Darken1);

                                col.Item().Text("Corporate Headquarters")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Medium);
                            });

                            row.ConstantItem(200).AlignRight().Column(col =>
                            {
                                col.Item().Text($"Reference: PC/{employee.EmployeeCode}/{DateTime.Now:yyyy}")
                                    .FontSize(9);

                                col.Item().Text($"Date: {DateTime.Now:dd MMMM yyyy}")
                                    .FontSize(9);
                            });
                        });

                        header.Item()
                            .PaddingTop(10)
                            .LineHorizontal(1.5f)
                            .LineColor(Colors.Black);
                    });

                    // ================= CONTENT =================
                    page.Content().PaddingTop(35).Column(content =>
                    {
                        content.Item().AlignCenter().Text("SALARY CERTIFICATE")
                            .Bold()
                            .FontSize(16)
                            .LetterSpacing(1);

                        content.Item().PaddingTop(5)
                            .AlignCenter()
                            .LineHorizontal(1);

                        content.Item().PaddingTop(35);

                        // Main Paragraph
                        content.Item().Text(text =>
                        {
                            text.Justify();

                            text.Span("This is to certify that ");
                            text.Span(employee.FullName).Bold();
                            text.Span(", holding Employee ID ");
                            text.Span(employee.EmployeeCode).Bold();
                            text.Span(", has been employed with ");
                            text.Span("Desai Global Industries").Bold();
                            text.Span(" since ");
                            text.Span(employee.HireDate.ToString("dd MMMM yyyy")).Bold();
                            text.Span(". ");
                            text.Span("He/She currently holds the position of ");
                            text.Span(employee.Role).Bold();
                            text.Span(" within the ");
                            text.Span(employee.Department).Bold();
                            text.Span(" division.");
                        });

                        content.Item().PaddingTop(20);

                        content.Item().Text(text =>
                        {
                            text.Justify();
                            text.Span("As per our official payroll records, the employee’s current gross monthly salary is ");
                            text.Span($"{salary.BaseSalary:N0} AED").Bold();
                            text.Span(" (UAE Dirhams ").Bold();
                            text.Span(NumberToWords((long)salary.BaseSalary)).Bold();
                            text.Span(" only), subject to applicable statutory deductions in accordance with Canadian federal and provincial regulations.");
                        });

                        content.Item().PaddingTop(30);

                        // Structured Info Section
                        content.Item().Border(1)
                            .Padding(15)
                            .Column(box =>
                            {
                                box.Item().Text("Employment Summary")
                                    .Bold()
                                    .FontSize(12);

                                box.Item().PaddingTop(10);

                                box.Item().Row(r =>
                                {
                                    r.RelativeItem().Text($"Employment Status: {employee.Status}");
                                    r.RelativeItem().Text($"Grade Level: {employee.Grade}");
                                });

                                box.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Work Location: Montreal, Quebec, Canada");
                                    r.RelativeItem().Text("Employment Type: Full-Time");
                                });
                            });

                        content.Item().PaddingTop(30);

                        content.Item().Text(text =>
                        {
                            text.Justify();
                            text.Span("This certificate is issued upon the request of the employee for official purposes and does not constitute a guarantee of continued employment. ");
                            text.Span("Desai Global Industries assumes no liability for any reliance placed upon this certificate beyond its stated purpose.");
                        });

                        content.Item().PaddingTop(50);

                        // Signature
                        content.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("For Desai Global Industries")
                                    .Bold();

                                col.Item().PaddingTop(40)
                                    .LineHorizontal(1);

                                col.Item().Text("Authorized Signatory")
                                    .FontSize(9);
                            });

                            row.RelativeItem();
                        });
                    });

                    // ================= FOOTER =================
                    page.Footer().AlignCenter().Column(footer =>
                    {
                        footer.Item().LineHorizontal(0.5f);

                        footer.Item().PaddingTop(5)
                            .Text("Desai Global Industries | Montreal, Quebec, Canada")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);

                        footer.Item()
                            .Text("PeopleCore Human Capital Platform – Confidential Document")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        }
    }
}