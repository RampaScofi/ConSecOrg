using ConSecOrg.Application.Features.Audit.Queries;
using ConSecOrg.Shared.DTOs.Audit;
using ConSecOrg.Shared.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Claims;
using System.Text;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/audit")]
[Authorize(Roles = "Admin,Auditor")]
public class AuditController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditLogDto>>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? userId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? action = null,
        CancellationToken ct = default)
        => Ok(await sender.Send(new GetAuditLogsQuery(page, pageSize, userId, from, to, action), ct));

    [HttpGet("verify")]
    public async Task<ActionResult<AuditChainVerifyResultDto>> Verify(CancellationToken ct)
        => Ok(await sender.Send(new VerifyAuditChainQuery(), ct));

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var result = await sender.Send(new GetAuditLogsQuery(1, 10000), ct);
        var csv = new StringBuilder();
        csv.AppendLine("SequenceNum,Timestamp,UserId,Action,EntityType,EntityId,Status,IpAddress");
        foreach (var l in result.Items)
            csv.AppendLine($"{l.SequenceNum},{l.Timestamp:O},{l.UserId},{l.Action},{l.EntityType},{l.EntityId},{l.Status},{l.IpAddress}");

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "audit_export.csv");
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var result = await sender.Send(new GetAuditLogsQuery(1, 10000, null, from, to), ct);
        var verifyResult = await sender.Send(new VerifyAuditChainQuery(), ct);

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(15, QuestPDF.Infrastructure.Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Arial"));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeContent(c, result.Items, verifyResult, from, to));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("ConSecOrg — Защищённый органайзер  |  Стр. ");
                    text.CurrentPageNumber();
                    text.Span(" из ");
                    text.TotalPages();
                    text.Span($"  |  Экспорт: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC");
                });
            });
        }).GeneratePdf();

        return File(pdfBytes, "application/pdf",
            $"audit_report_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf");
    }

    private static void ComposeHeader(IContainer container)
    {
        container.BorderBottom(1).BorderColor(Colors.Grey.Darken2).PaddingBottom(4).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("ЖУРНАЛ АУДИТА БЕЗОПАСНОСТИ")
                    .FontSize(14).Bold().FontColor(Colors.Grey.Darken3);
                col.Item().Text("Система ConSecOrg — Защищённый электронный органайзер")
                    .FontSize(9).FontColor(Colors.Grey.Medium);
            });
            row.ConstantItem(180).AlignRight().Column(col =>
            {
                col.Item().Text($"Дата: {DateTime.UtcNow:dd.MM.yyyy}").FontSize(8);
                col.Item().Text($"Время (UTC): {DateTime.UtcNow:HH:mm:ss}").FontSize(8);
                col.Item().Text("Конфиденциально").FontSize(8).Bold().FontColor(Colors.Orange.Medium);
            });
        });
    }

    private static void ComposeContent(IContainer container,
        IReadOnlyList<AuditLogDto> logs,
        AuditChainVerifyResultDto verify,
        DateTime? from, DateTime? to)
    {
        container.Column(col =>
        {
            // Chain integrity banner
            col.Item().Background(verify.IsIntact ? Colors.Green.Lighten4 : Colors.Red.Lighten4)
                .Border(1)
                .BorderColor(verify.IsIntact ? Colors.Green.Medium : Colors.Red.Medium)
                .Padding(6)
                .Row(row =>
                {
                    row.AutoItem().Text(verify.IsIntact ? "✓" : "✗")
                        .FontSize(12).Bold()
                        .FontColor(verify.IsIntact ? Colors.Green.Darken2 : Colors.Red.Darken2);
                    row.RelativeItem().PaddingLeft(6).Text(text =>
                    {
                        text.Span("Целостность хэш-цепочки: ").Bold();
                        text.Span(verify.IsIntact
                            ? $"ПОДТВЕРЖДЕНА — проверено {verify.TotalRecordsChecked} записей"
                            : $"НАРУШЕНА в записи #{verify.TamperedAtSequence}").Bold()
                            .FontColor(verify.IsIntact ? Colors.Green.Darken3 : Colors.Red.Darken3);
                    });
                });

            col.Item().Height(4);

            // Filters summary
            if (from.HasValue || to.HasValue)
            {
                col.Item().PaddingBottom(4).Text(text =>
                {
                    text.Span("Период: ").Bold();
                    text.Span(from.HasValue ? from.Value.ToString("dd.MM.yyyy") : "начало");
                    text.Span(" — ");
                    text.Span(to.HasValue ? to.Value.ToString("dd.MM.yyyy") : "настоящее время");
                    text.Span($" | Записей: {logs.Count}");
                });
            }
            else
            {
                col.Item().PaddingBottom(4).Text($"Всего записей: {logs.Count}");
            }

            // Table
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);   // #
                    columns.RelativeColumn(2.2f); // Timestamp
                    columns.RelativeColumn(2f);   // Action
                    columns.RelativeColumn(1.5f); // EntityType
                    columns.RelativeColumn(2.2f); // EntityId
                    columns.RelativeColumn(1f);   // Status
                    columns.RelativeColumn(1.8f); // IP
                    columns.RelativeColumn(2.5f); // UserId
                });

                // Header row
                static IContainer HeaderCell(IContainer c) =>
                    c.Background(Colors.Grey.Darken2).Padding(4).AlignCenter();
                static void HeaderText(TextDescriptor t) =>
                    t.DefaultTextStyle(x => x.Bold().FontColor(Colors.White).FontSize(7.5f));

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text(t => { HeaderText(t); t.Span("#"); });
                    header.Cell().Element(HeaderCell).Text(t => { HeaderText(t); t.Span("Дата / Время (UTC)"); });
                    header.Cell().Element(HeaderCell).Text(t => { HeaderText(t); t.Span("Действие"); });
                    header.Cell().Element(HeaderCell).Text(t => { HeaderText(t); t.Span("Тип объекта"); });
                    header.Cell().Element(HeaderCell).Text(t => { HeaderText(t); t.Span("ID объекта"); });
                    header.Cell().Element(HeaderCell).Text(t => { HeaderText(t); t.Span("Статус"); });
                    header.Cell().Element(HeaderCell).Text(t => { HeaderText(t); t.Span("IP адрес"); });
                    header.Cell().Element(HeaderCell).Text(t => { HeaderText(t); t.Span("ID пользователя"); });
                });

                // Data rows
                var rowIdx = 0;
                foreach (var log in logs)
                {
                    var isEven = rowIdx++ % 2 == 0;
                    var bg = isEven ? Colors.White : Colors.Grey.Lighten5;
                    var statusColor = log.Status == "Success" ? Colors.Green.Darken2 : Colors.Red.Darken2;

                    static IContainer DataCell(IContainer c, string bg) =>
                        c.Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3);

                    table.Cell().Element(c => DataCell(c, bg)).AlignCenter()
                        .Text(log.SequenceNum.ToString()).FontSize(7);
                    table.Cell().Element(c => DataCell(c, bg))
                        .Text(log.Timestamp.ToString("dd.MM.yy HH:mm:ss")).FontSize(7);
                    table.Cell().Element(c => DataCell(c, bg))
                        .Text(log.Action).FontSize(7).Bold();
                    table.Cell().Element(c => DataCell(c, bg))
                        .Text(log.EntityType ?? "—").FontSize(7);
                    table.Cell().Element(c => DataCell(c, bg))
                        .Text(TruncateId(log.EntityId)).FontSize(6.5f);
                    table.Cell().Element(c => DataCell(c, bg)).AlignCenter()
                        .Text(log.Status).FontSize(7).FontColor(statusColor).Bold();
                    table.Cell().Element(c => DataCell(c, bg))
                        .Text(log.IpAddress ?? "—").FontSize(7);
                    table.Cell().Element(c => DataCell(c, bg))
                        .Text(TruncateId(log.UserId?.ToString())).FontSize(6.5f);
                }
            });
        });
    }

    private static string TruncateId(string? id) =>
        string.IsNullOrEmpty(id) ? "—" : id.Length > 16 ? id[..8] + "…" + id[^4..] : id;
}
