using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chatex.Api.Tests;

internal static class HttpAssertions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<T> ShouldHaveAsync<T>(this HttpResponseMessage response, HttpStatusCode status)
        where T : class
    {
        await response.ShouldHaveStatusAsync(status);
        var body = await response.Content.ReadFromJsonAsync<T>(Json, Ct);
        body.ShouldNotBeNull();
        return body;
    }

    public static async Task ShouldHaveStatusAsync(this HttpResponseMessage response, HttpStatusCode status)
    {
        if (response.StatusCode != status)
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            response.StatusCode.ShouldBe(status, $"Body: {body}");
        }
    }

    /// <summary>Checks a problem details response and returns its body.</summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        await response.ShouldHaveStatusAsync(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("status").GetInt32().ShouldBe((int)status);
        if (code is not null)
        {
            problem.GetProperty("code").GetString().ShouldBe(code);
        }

        return problem;
    }

    /// <summary>The value of a cookie the response sets, with its attributes.</summary>
    public static string? SetCookie(this HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(name + "=", StringComparison.Ordinal))
            : null;

    public static string CookieValue(string setCookie) => setCookie.Split(';')[0].Split('=', 2)[1];
}
