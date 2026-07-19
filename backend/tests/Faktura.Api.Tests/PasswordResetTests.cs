using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Faktura.Api.Features.Auth;
using Faktura.Api.Tests.Fakes;
using Faktura.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>Spec 011: enumereringssäkert glömt lösenord-flöde.</summary>
public class PasswordResetTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public PasswordResetTests(FakturaApiFactory factory) => _factory = factory;

    private FakeEmailSender Sender => (FakeEmailSender)_factory.Services.GetRequiredService<IEmailSender>();

    private static string ExtractToken(string body)
        => Regex.Match(body, @"/reset/([A-Za-z0-9_\-]+)").Groups[1].Value;

    [Fact]
    public async Task Full_reset_flow_rotates_password_and_kills_sessions()
    {
        var client = _factory.CreateClient();
        var auth = (await (await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("ResetOrg", "reset-1@acme.se", "password1"))).Content
            .ReadFromJsonAsync<AuthResponse>())!;

        // Begär återställning — 202 + mejl med länk.
        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("reset-1@acme.se"));
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        Assert.Equal("reset-1@acme.se", Sender.LastMessage!.To);
        var token = ExtractToken(Sender.LastMessage.Body);
        Assert.False(string.IsNullOrEmpty(token));

        // Sätt nytt lösenord.
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(token, "nyttpass9"));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // Gammalt lösenord nekas, nytt fungerar.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("reset-1@acme.se", "password1"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("reset-1@acme.se", "nyttpass9"))).StatusCode);

        // Gamla refresh-token är återkallad (stulna sessioner dör).
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken))).StatusCode);

        // Engångs: samma länk igen nekas.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(token, "annatpass8"))).StatusCode);
    }

    [Fact]
    public async Task Unknown_email_gets_generic_202_without_mail_and_throttle_is_silent()
    {
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("ThrOrg", "reset-thr@acme.se", "password1"))).EnsureSuccessStatusCode();

        // Okänd adress: 202 utan mejl.
        var before = Sender.SentCount;
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("finns-inte@acme.se"));
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(before, Sender.SentCount);

        // Känd adress: bromsen slår till efter MaxAttempts=3 — svaret förblir 202 men inget mejl.
        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("reset-thr@acme.se"));
        var mailsAfterThree = Sender.SentCount;

        var fourth = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("reset-thr@acme.se"));
        Assert.Equal(HttpStatusCode.Accepted, fourth.StatusCode);   // fortfarande generiskt
        Assert.Equal(mailsAfterThree, Sender.SentCount);            // men inget nytt mejl
    }

    [Fact]
    public async Task Reset_validates_password_policy_and_bogus_token()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest("whatever", "svag"))).StatusCode);          // policy
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest("bogus-token", "giltigt9pass"))).StatusCode); // ogiltig token
    }
}
