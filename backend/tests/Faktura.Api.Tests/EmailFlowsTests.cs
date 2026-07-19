using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Members;
using Faktura.Api.Tests.Fakes;
using Faktura.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>Spec 010: registreringsbroms + varningsmejl (A3) och mejlade inbjudningar (A4).</summary>
public class EmailFlowsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public EmailFlowsTests(FakturaApiFactory factory) => _factory = factory;

    private FakeEmailSender Sender => (FakeEmailSender)_factory.Services.GetRequiredService<IEmailSender>();

    private static RegisterRequest Reg(string email, string org = "Acme") => new(org, email, "password1");

    [Fact]
    public async Task Duplicate_registration_warns_owner_and_throttles_to_429()
    {
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/register", Reg("flow-dup@acme.se", "Flow1"))).EnsureSuccessStatusCode();

        // Försök 1–3 mot upptagen adress: 409 + varningsmejl till adressens ägare.
        for (var i = 0; i < 3; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/auth/register", Reg("flow-dup@acme.se", "Intrång"));
            Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        }
        var warning = Sender.LastMessage!;
        Assert.Equal("flow-dup@acme.se", warning.To);
        Assert.Contains("Registreringsförsök", warning.Subject);

        // Fjärde försöket: bromsen slår till (MaxAttempts=3 i testkonfig).
        var blocked = await client.PostAsJsonAsync("/api/auth/register", Reg("flow-dup@acme.se", "Intrång"));
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);

        // Nya (lediga) adresser är opåverkade.
        var fresh = await client.PostAsJsonAsync("/api/auth/register", Reg("flow-fresh@acme.se", "Flow2"));
        Assert.Equal(HttpStatusCode.Created, fresh.StatusCode);
    }

    [Fact]
    public async Task Invitation_is_emailed_with_accept_link()
    {
        var client = _factory.CreateClient();
        var auth = (await (await client.PostAsJsonAsync("/api/auth/register", Reg("flow-inv@acme.se", "InvOrg"))).Content
            .ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var resp = await client.PostAsJsonAsync("/api/invitations", new InviteRequest("kollega@acme.se", "Member"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var invite = (await resp.Content.ReadFromJsonAsync<InviteResponse>())!;

        var mail = Sender.LastMessage!;
        Assert.Equal("kollega@acme.se", mail.To);
        Assert.Contains($"/accept/{invite.Token}", mail.Body);           // länken bär accept-token
        Assert.Contains("InvOrg", mail.Subject);
        Assert.Equal(auth.User.Email, mail.ReplyTo);                     // svar går till inbjudaren

        // Länken fungerar: den inbjudna kan acceptera.
        var accept = await _factory.CreateClient()
            .PostAsJsonAsync($"/api/invitations/{invite.Token}/accept", new AcceptInvitationRequest("password1"));
        Assert.Equal(HttpStatusCode.Created, accept.StatusCode);
    }

    [Fact]
    public async Task Invitation_survives_email_failure()
    {
        var client = _factory.CreateClient();
        var auth = (await (await client.PostAsJsonAsync("/api/auth/register", Reg("flow-mailfail@acme.se", "MfOrg"))).Content
            .ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // FakeEmailSender kastar för fail@-adresser — inbjudan ska ändå skapas (FR-003).
        var resp = await client.PostAsJsonAsync("/api/invitations", new InviteRequest("fail@acme.se", "Member"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var invitations = await client.GetFromJsonAsync<List<InvitationDto>>("/api/invitations");
        Assert.Contains(invitations!, i => i.Email == "fail@acme.se" && i.Status == "Pending");
    }
}
