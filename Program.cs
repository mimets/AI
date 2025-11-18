using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

class Program
{
    private static readonly string apiKey = "API";
    private static readonly string apiUrl = "https://api.perplexity.ai/chat/completions";
    private static readonly string defaultModel = "sonar";

    private static string lastCodeBlock = "";
    private static string currentVerbosity = "compact";

    static async Task Main(string[] args)
    {
        Console.Title = "Chat AI Console";
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Clear();

        PrintHeader();

        var manager = new ChatManager(apiKey, apiUrl, defaultModel);
        manager.LoadHistory();

        PrintSeparator();
        manager.ShowHelp();

        while (true)
        {
            PrintMode();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\n► Tu: ");
            Console.ResetColor();

            string input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.StartsWith("/"))
            {
                HandleCommand(input, manager);
                continue;
            }

            // Auto-switch verbosità in base al tipo di domanda
            string questionType = AiHelper.DetectQuestionType(input);
            if (questionType == "theory" && currentVerbosity != "verbose")
            {
                currentVerbosity = "verbose";
                manager.SetVerbosity("verbose");
                PrintMode();
            }
            else if ((questionType == "math" || questionType == "code") && currentVerbosity != "compact")
            {
                currentVerbosity = "compact";
                manager.SetVerbosity("compact");
                PrintMode();
            }

            try
            {
                Console.WriteLine();

                var stopwatch = Stopwatch.StartNew();
                var task = manager.SendMessageAsync(input);

                // Barra di caricamento indeterminata
                int barWidth = 30;
                int stepDelay = 80;
                int position = 0;

                int progressTop = Console.CursorTop;
                bool showedJoke = false;

                while (!task.IsCompleted)
                {
                    Console.SetCursorPosition(0, progressTop);
                    DrawLoadingBar(position, barWidth);

                    position++;
                    if (position >= barWidth)
                        position = 0;

                    // Dopo 8 secondi mostra un messaggio simpatico (una sola volta)
                    if (!showedJoke && stopwatch.ElapsedMilliseconds > 8000)
                    {
                        Console.SetCursorPosition(0, progressTop + 1);
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write("💡 Domanda impegnativa, ci sto pensando per bene...");
                        Console.ResetColor();
                        showedJoke = true;
                    }

                    System.Threading.Thread.Sleep(stepDelay);
                }

                // quando ha finito: pulisci barra e messaggio
                Console.SetCursorPosition(0, progressTop);
                Console.Write(new string(' ', barWidth + 40));
                Console.SetCursorPosition(0, progressTop + 1);
                Console.Write(new string(' ', 80));
                Console.SetCursorPosition(0, progressTop);

                var reply = await task;
                stopwatch.Stop();

                string codeBlock = "";
                if (reply.Contains("[CODICE_START]") && reply.Contains("[CODICE_END]"))
                {
                    int startIdx = reply.IndexOf("[CODICE_START]") + "[CODICE_START]".Length;
                    int endIdx = reply.IndexOf("[CODICE_END]");
                    codeBlock = reply.Substring(startIdx, endIdx - startIdx).Trim();
                    reply = reply.Substring(0, reply.IndexOf("[CODICE_START]")).Trim();
                }

                PrintAIResponse(reply);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n⏱ Tempo risposta: {stopwatch.ElapsedMilliseconds} ms");
                Console.ResetColor();

                if (!string.IsNullOrEmpty(codeBlock))
                {
                    lastCodeBlock = codeBlock;
                    PrintCodeBox(codeBlock, Console.WindowWidth);
                }
                else if (IsCodeBlock(reply))
                {
                    lastCodeBlock = reply;
                    PrintCodeBox(reply, Console.WindowWidth);
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n────────────────────────────────────────");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ Errore: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("CHAT AI CONSOLE - Federico Furlani");
        Console.ResetColor();
        Console.WriteLine();
    }

    static void PrintSeparator()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("─────────────────────────────────────────────────────");
        Console.ResetColor();
    }

    static void PrintMode()
    {
        int origLeft = Console.CursorLeft;
        int origTop = Console.CursorTop;

        string modeText = $"[{currentVerbosity.ToUpper()}]";
        int rightPos = Math.Max(0, Console.WindowWidth - modeText.Length - 1);

        Console.SetCursorPosition(rightPos, 0);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(modeText);
        Console.ResetColor();

        Console.SetCursorPosition(origLeft, origTop);
    }

    static void PrintAIResponse(string reply)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"[{DateTime.Now:HH:mm}] ");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("◄ AI: ");
        Console.ResetColor();

        int consoleWidth = Console.WindowWidth;
        int maxWidth = consoleWidth - 20; // margine ampio per evitare la scrollbar

        string cleanReply = AiHelper.NormalizeForConsole(reply);

        Console.ForegroundColor = ConsoleColor.Green;

        string indent = "        ";

        if (cleanReply.Contains("=") && (cleanReply.Contains("x") || cleanReply.Contains("y")))
        {
            var sentences = cleanReply.Split(new[] { ". ", ".\n", ": " }, StringSplitOptions.RemoveEmptyEntries);

            bool firstBlock = true;

            foreach (var sentence in sentences)
            {
                string trimmed = sentence.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.Contains("="))
                {
                    if (!firstBlock)
                        Console.Write(indent);

                    WrapAndWrite(trimmed, maxWidth, indent, firstBlock);
                    firstBlock = false;
                }
                else
                {
                    Console.Write(indent);
                    WrapAndWrite(trimmed, maxWidth, indent, false);
                }
            }
        }
        else
        {
            WrapAndWrite(cleanReply, maxWidth, indent, true);
        }

        Console.ResetColor();
    }

    static void WrapAndWrite(string text, int maxWidth, string indent, bool isFirstLine)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string line = "";

        foreach (var word in words)
        {
            string candidate = line.Length > 0 ? line + " " + word : word;

            if (candidate.Length > maxWidth)
            {
                Console.WriteLine(line);

                Console.Write(indent);
                line = word;
                isFirstLine = false;
            }
            else
            {
                line = candidate;
            }
        }

        if (!string.IsNullOrEmpty(line))
            Console.WriteLine(line);
    }

    static bool IsCodeBlock(string text)
    {
        if (text.Contains("\\(") || text.Contains("\\frac") || text.Contains("\\)"))
            return false;

        if (text.Contains("=") && !text.Contains("def ") && !text.Contains("{") &&
            !text.Contains("print") && !text.Contains("return"))
            return false;

        int codeIndicators = 0;

        if (text.Contains("def ")) codeIndicators++;
        if (text.Contains("return ")) codeIndicators++;
        if (text.Contains("public ")) codeIndicators++;
        if (text.Contains("private ")) codeIndicators++;
        if (text.Contains("for ") || text.Contains("while ")) codeIndicators++;
        if (text.Contains("{") && text.Contains("}")) codeIndicators++;
        if (text.Contains("print(") || text.Contains("println")) codeIndicators++;
        if (text.Contains("//") || text.Contains("#")) codeIndicators++;
        if (text.Contains("import ")) codeIndicators++;
        if (text.Contains("class ")) codeIndicators++;

        return codeIndicators >= 3;
    }

    static void PrintCodeBox(string code, int consoleWidth)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;

        int boxWidth = Math.Min(consoleWidth - 2, 110);
        if (boxWidth < 40) boxWidth = 40;

        string topBorder = "╔" + new string('═', boxWidth - 2) + "╗";
        string bottomBorder = "╚" + new string('═', boxWidth - 2) + "╝";
        string copyHint = " [/copy] ";

        Console.WriteLine("\n" + topBorder);
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("║ 📋 CODE");
        Console.ForegroundColor = ConsoleColor.Yellow;
        int rightPad = boxWidth - copyHint.Length - 11;
        if (rightPad > 0) Console.Write(new string(' ', rightPad));
        Console.Write(copyHint);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("║");
        Console.WriteLine("╠" + new string('═', boxWidth - 2) + "╣");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Green;
        int contentWidth = boxWidth - 6;
        int indent = 0;

        string formattedCode = code
            .Replace("{ ", "{\n")
            .Replace(" {", "\n{")
            .Replace("};", "}\n;")
            .Replace("}; ", "}\n;\n")
            .Replace("}:\n", "}\n:\n")
            .Replace("; ", ";\n")
            .Replace(":\n", ":\n")
            .Replace("for ", "\nfor ")
            .Replace("while ", "\nwhile ")
            .Replace("if ", "\nif ");

        var lines = formattedCode.Split('\n');

        foreach (var line in lines)
        {
            string cleanLine = line.Trim();
            if (string.IsNullOrWhiteSpace(cleanLine)) continue;

            if (cleanLine.StartsWith("}")) indent = Math.Max(0, indent - 1);

            string indentStr = new string(' ', indent * 2);
            string fullLine = indentStr + cleanLine;

            while (fullLine.Length > contentWidth)
            {
                Console.Write("║  ");
                Console.Write(fullLine.Substring(0, contentWidth));
                Console.WriteLine("  ║");
                fullLine = "    " + fullLine.Substring(contentWidth).TrimStart();
            }

            Console.Write("║  ");
            Console.Write(fullLine.PadRight(contentWidth));
            Console.WriteLine("  ║");

            if (cleanLine.EndsWith("{")) indent++;
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(bottomBorder);
        Console.ResetColor();
    }

    static void DrawLoadingBar(int position, int width)
    {
        if (width < 5) width = 5;
        position %= width;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("⟳ ");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("[");
        for (int i = 0; i < width; i++)
        {
            if (i == position)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("█");
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }
            else
            {
                Console.Write("─");
            }
        }
        Console.Write("]");
        Console.ResetColor();
    }

    static void HandleCommand(string command, ChatManager manager)
    {
        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLower();

        switch (cmd)
        {
            case "/copy":
            case "/cp":
                if (!string.IsNullOrEmpty(lastCodeBlock))
                {
                    try
                    {
                        string tempFile = Path.GetTempFileName();
                        File.WriteAllText(tempFile, lastCodeBlock);

                        using var process = new Process();
                        process.StartInfo.FileName = "cmd.exe";
                        process.StartInfo.Arguments = $"/c type \"{tempFile}\" | clip";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        process.WaitForExit();

                        File.Delete(tempFile);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n✓ Codice copiato negli appunti!");
                        Console.ResetColor();
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\n⚠ Impossibile copiare. Seleziona il testo e premi Ctrl+C");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n⚠ Nessun codice da copiare");
                    Console.ResetColor();
                }
                break;

            case "/verbose":
            case "/v":
                currentVerbosity = "verbose";
                manager.SetVerbosity("verbose");
                PrintMode();
                break;

            case "/compact":
            case "/c":
                currentVerbosity = "compact";
                manager.SetVerbosity("compact");
                PrintMode();
                break;

            case "/clear":
            case "/cls":
                currentVerbosity = "compact";
                manager.ClearChat();
                Console.Clear();
                PrintHeader();
                PrintSeparator();
                manager.ShowHelp();
                break;

            case "/save":
            case "/s":
                manager.SaveHistory();
                break;

            case "/load":
            case "/l":
                manager.LoadHistory();
                break;

            case "/model":
            case "/m":
                if (parts.Length > 1)
                    manager.SetModel(parts[1]);
                else
                    Console.WriteLine("\nUso: /model [nome_modello]");
                break;

            case "/help":
            case "/h":
            case "/?":
                PrintSeparator();
                manager.ShowHelp();
                break;

            case "/exit":
            case "/quit":
            case "/q":
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\nArrivederci! 👋");
                Console.ResetColor();
                Environment.Exit(0);
                break;

            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ Comando sconosciuto: {cmd}");
                Console.ResetColor();
                break;
        }
    }
}
