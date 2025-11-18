using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;

public class ChatManager
{
    private readonly string apiKey;
    private readonly string apiUrl;
    private string modelName;
    private List<ChatMessage> messages;
    private readonly string historyFile = "chat_history.json";
    private string currentVerbosity = "compact";

    private readonly Dictionary<string, string> themes = new()
    {
        {
            "verbose",
            "Rispondi sempre in italiano con spiegazioni complete e dettagliate. " +
            "Per il codice, spiega le parti principali. Per la matematica, illustra i passaggi. " +
            "Usa uno stile chiaro, ordinato, con frasi complete."
        },
        {
            "compact",
            "Rispondi sempre in italiano in modo molto sintetico (max 2-3 righe). " +
            "Vai dritto al punto. Per il codice, mostra solo il codice essenziale. " +
            "Per la matematica, indica solo i passaggi necessari e il risultato."
        }
    };

    public ChatManager(string key, string url, string defaultModel = "sonar")
    {
        apiKey = key;
        apiUrl = url;
        modelName = defaultModel;

        messages = new List<ChatMessage>
        {
            new ChatMessage { role = "system", content = themes["compact"] }
        };
    }

    public void SetVerbosity(string mode)
    {
        if (!themes.ContainsKey(mode.ToLower()))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Modalità '{mode}' non trovata. Usa: verbose, compact");
            Console.ResetColor();
            return;
        }

        currentVerbosity = mode.ToLower();
        messages[0] = new ChatMessage { role = "system", content = themes[currentVerbosity] };

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Modalità cambiata a: {currentVerbosity.ToUpper()}");
        Console.ResetColor();
    }

    public string GetVerbosity() => currentVerbosity;

    public void SetModel(string model)
    {
        modelName = model;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Modello cambiato a: {modelName}");
        Console.ResetColor();
    }

    public void ClearChat()
    {
        string systemMessage = messages[0].content;
        messages.Clear();
        messages.Add(new ChatMessage { role = "system", content = systemMessage });

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Chat cancellata (contesto interno pulito).");
        Console.ResetColor();
    }

    public void SaveHistory()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(messages, options);
            File.WriteAllText(historyFile, json, Encoding.UTF8);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Chat salvata in '{historyFile}'");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Errore salvataggio: {ex.Message}");
            Console.ResetColor();
        }
    }

    public void LoadHistory()
    {
        try
        {
            if (!File.Exists(historyFile))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Nessuna chat precedente. Nuova sessione.");
                Console.ResetColor();
                return;
            }

            string json = File.ReadAllText(historyFile, Encoding.UTF8);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            messages = JsonSerializer.Deserialize<List<ChatMessage>>(json, options) ?? new List<ChatMessage>();

            if (messages.Count == 0 || messages[0].role != "system")
            {
                messages.Insert(0, new ChatMessage { role = "system", content = themes[currentVerbosity] });
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Chat caricata ({messages.Count} messaggi)");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Errore caricamento: {ex.Message}");
            Console.ResetColor();
        }
    }

    private (string text, string codeBlock) CleanMarkdownAndExtractCode(string text)
    {
        if (string.IsNullOrEmpty(text))
            return (text, "");

        string codeBlock = "";

        var codeMatch = Regex.Match(text, @"``````", RegexOptions.IgnoreCase);
        if (codeMatch.Success)
        {
            codeBlock = codeMatch.Groups[1].Value.Trim();
            text = Regex.Replace(text, @"``````", "", RegexOptions.IgnoreCase);
        }

        text = Regex.Replace(text, @"\[([^\]]+)\]\([^\)]+\)", "$1");
        text = Regex.Replace(text, @"\[\d+\]", "");
        text = text.Replace("**", "").Replace("__", "").Replace("`", "");
        text = Regex.Replace(text, @"\*([^\*]+)\*", "$1");
        text = Regex.Replace(text, @"^#+\s+", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"\s+", " ");

        return (text.Trim(), codeBlock);
    }

    public async Task<string> SendMessageAsync(string userInput)
    {
        messages.Add(new ChatMessage { role = "user", content = userInput });

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var request = new ChatRequest
        {
            model = modelName,
            messages = messages,
            // max_tokens omesso: lascia al modello il limite massimo
            temperature = 0.5f
        };

        string json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await http.PostAsync(apiUrl, content);
        string respText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)response.StatusCode}: {respText}");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var chatResponse = JsonSerializer.Deserialize<ChatResponse>(respText, options);
        string reply = chatResponse?.choices?[0]?.message?.content ?? "(nessuna risposta)";

        var (cleanedReply, codeBlock) = CleanMarkdownAndExtractCode(reply);
        messages.Add(new ChatMessage { role = "assistant", content = cleanedReply });

        if (!string.IsNullOrEmpty(codeBlock))
            return cleanedReply + "\n\n[CODICE_START]" + codeBlock + "[CODICE_END]";

        return cleanedReply;
    }

    public void ShowHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n=== COMANDI ===");
        Console.WriteLine("/clear   /cls   - Cancella chat");
        Console.WriteLine("/save    /s     - Salva cronologia");
        Console.WriteLine("/load    /l     - Carica cronologia");
        Console.WriteLine("/verbose /v     - Risposte dettagliate");
        Console.WriteLine("/compact /c     - Risposte brevi (default)");
        Console.WriteLine("/copy    /cp    - Copia l'ultimo codice");
        Console.WriteLine("/model   /m     - Cambia modello (/model nome)");
        Console.WriteLine("/help    /h /?  - Aiuto");
        Console.WriteLine("/exit    /quit /q - Esci");
        Console.WriteLine("==============\n");
        Console.ResetColor();
    }

    public class ChatMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class ChatRequest
    {
        public string model { get; set; }
        public List<ChatMessage> messages { get; set; }
        public int? max_tokens { get; set; }   // opzionale, ma NON lo usiamo
        public float temperature { get; set; }
    }

    public class ChatResponse
    {
        public List<Choice> choices { get; set; }

        public class Choice
        {
            public ChatMessage message { get; set; }
        }
    }
}
