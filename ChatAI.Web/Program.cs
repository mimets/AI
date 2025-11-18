using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Abilita CORS semplice per poterci accedere da ovunque (telefono, ecc.)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Registriamo ChatManager come singleton
builder.Services.AddSingleton(sp =>
{
    string apiKey = "KEY";
    string apiUrl = "https://api.perplexity.ai/chat/completions";
    string defaultModel = "sonar";

    return new ChatManager(apiKey, apiUrl, defaultModel);
});

var app = builder.Build();

app.UseCors();

// Serve il file index.html statico
app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoint API: /ask
app.MapPost("/ask", async (HttpContext http, ChatManager manager) =>
{
    try
    {
        var body = await http.Request.ReadFromJsonAsync<AskRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Message))
        {
            http.Response.StatusCode = 400;
            await http.Response.WriteAsJsonAsync(new { error = "Messaggio vuoto" });
            return;
        }

        string reply = await manager.SendMessageAsync(body.Message);
        await http.Response.WriteAsJsonAsync(new AskResponse
        {
            Reply = reply
        });
    }
    catch (Exception ex)
    {
        http.Response.StatusCode = 500;
        await http.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

app.Run();

public class AskRequest
{
    public string Message { get; set; }
}

public class AskResponse
{
    public string Reply { get; set; }
}
