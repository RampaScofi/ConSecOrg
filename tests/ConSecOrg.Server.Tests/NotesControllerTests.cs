using System.Net;
using System.Net.Http.Json;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Enums;
using ConSecOrg.Shared.Pagination;
using FluentAssertions;

namespace ConSecOrg.Server.Tests;

public class NotesControllerTests(WebApiFactory factory) : IClassFixture<WebApiFactory>
{
    // ── tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotes_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/notes");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateNote_ValidData_Returns201()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/notes",
            new CreateNoteRequestDto
            {
                Title = "Integration test note",
                Content = "Content for testing",
                SecurityLevel = SecurityLevelDto.Internal
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetNotes_AfterCreate_ReturnsTheNote()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        const string title = "Searchable note for GET test";

        await client.PostAsJsonAsync("/api/v1/notes",
            new CreateNoteRequestDto { Title = title, Content = "body", SecurityLevel = SecurityLevelDto.Public });

        var response = await client.GetAsync("/api/v1/notes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<NoteDto>>();
        paged.Should().NotBeNull();
        paged!.Items.Should().Contain(n => n.Title == title);
    }

    [Fact]
    public async Task GetNoteById_ExistingNote_Returns200()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/v1/notes",
            new CreateNoteRequestDto { Title = "ById note", Content = "text", SecurityLevel = SecurityLevelDto.Internal });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var noteId = await createResp.Content.ReadFromJsonAsync<Guid>();

        var getResp = await client.GetAsync($"/api/v1/notes/{noteId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var note = await getResp.Content.ReadFromJsonAsync<NoteDto>();
        note!.Title.Should().Be("ById note");
    }

    [Fact]
    public async Task GetNoteById_OtherUsersNote_Returns403()
    {
        // User A creates a note
        var (clientA, _) = await factory.CreateAuthenticatedClientAsync();
        var createResp = await clientA.PostAsJsonAsync("/api/v1/notes",
            new CreateNoteRequestDto { Title = "Private note", Content = "secret", SecurityLevel = SecurityLevelDto.Confidential });
        var noteId = await createResp.Content.ReadFromJsonAsync<Guid>();

        // User B tries to read it — server throws ForbiddenException (not NotFoundException)
        var (clientB, _) = await factory.CreateAuthenticatedClientAsync();
        var getResp = await clientB.GetAsync($"/api/v1/notes/{noteId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteNote_OwnNote_Returns204()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/v1/notes",
            new CreateNoteRequestDto { Title = "Delete me", Content = "bye", SecurityLevel = SecurityLevelDto.Public });
        var noteId = await createResp.Content.ReadFromJsonAsync<Guid>();

        var deleteResp = await client.DeleteAsync($"/api/v1/notes/{noteId}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SearchNotes_MatchingQuery_ReturnsResults()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        const string uniqueWord = "uniqueSearchToken_abc";

        await client.PostAsJsonAsync("/api/v1/notes",
            new CreateNoteRequestDto { Title = $"Note with {uniqueWord}", Content = "body", SecurityLevel = SecurityLevelDto.Internal });

        var response = await client.GetAsync($"/api/v1/notes/search?q={uniqueWord}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<IReadOnlyList<NoteDto>>();
        results.Should().NotBeEmpty();
    }
}
