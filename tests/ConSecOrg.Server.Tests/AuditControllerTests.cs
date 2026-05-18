using System.Net;
using System.Net.Http.Json;
using ConSecOrg.Shared.DTOs.Audit;
using FluentAssertions;

namespace ConSecOrg.Server.Tests;

public class AuditControllerTests(WebApiFactory factory) : IClassFixture<WebApiFactory>
{
    private Task<HttpClient> AdminClientAsync() => factory.CreateAdminClientAsync();

    // ── tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditLogs_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/audit");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuditLogs_RegularUser_Returns403()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/v1/audit");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAuditLogs_AdminRole_Returns200()
    {
        var adminClient = await AdminClientAsync();

        var response = await adminClient.GetAsync("/api/v1/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VerifyChain_AdminRole_ReturnsIntact()
    {
        var adminClient = await AdminClientAsync();

        var response = await adminClient.GetAsync("/api/v1/audit/verify");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuditChainVerifyResultDto>();
        result.Should().NotBeNull();
        result!.IsIntact.Should().BeTrue($"hash chain should be valid (TamperedAt=#{result.TamperedAtSequence}, TotalRecords={result.TotalRecordsChecked})");
    }

    [Fact]
    public async Task ExportCsv_AdminRole_ReturnsCsvFile()
    {
        var adminClient = await AdminClientAsync();

        var response = await adminClient.GetAsync("/api/v1/audit/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");

        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("SequenceNum");  // CSV header row
    }

    [Fact]
    public async Task ExportPdf_AdminRole_ReturnsPdfFile()
    {
        var adminClient = await AdminClientAsync();

        var response = await adminClient.GetAsync("/api/v1/audit/export/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
        // PDF files start with "%PDF"
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task VerifyChain_AfterLogin_ChainStillIntact()
    {
        // Trigger several audit log writes via login events
        var (_, _) = await factory.CreateAuthenticatedClientAsync();
        var (_, _) = await factory.CreateAuthenticatedClientAsync();

        var adminClient = await AdminClientAsync();
        var response = await adminClient.GetAsync("/api/v1/audit/verify");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AuditChainVerifyResultDto>();
        result!.IsIntact.Should().BeTrue($"chain must remain intact after normal login audit entries (TamperedAt=#{result.TamperedAtSequence}, TotalRecords={result.TotalRecordsChecked})");
    }
}
