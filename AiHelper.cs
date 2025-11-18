using System;

public static class AiHelper
{
    public static string DetectQuestionType(string input)
    {
        string lower = input.ToLower();

        if (IsMathQuestion(lower)) return "math";
        if (IsCodeQuestion(lower)) return "code";
        if (IsTheoryQuestion(lower)) return "theory";

        return "generic";
    }

    public static bool IsMathQuestion(string input)
    {
        return input.Contains("=") ||
               input.Contains("+") || input.Contains("-") ||
               input.Contains("*") || input.Contains("/") ||
               input.Contains("x") || input.Contains("y") ||
               input.Contains("calcola") || input.Contains("risolvi") ||
               input.Contains("equazione") || input.Contains("frazione");
    }

    public static bool IsCodeQuestion(string input)
    {
        return input.Contains("codice") || input.Contains("code") ||
               input.Contains("script") || input.Contains("programma") ||
               input.Contains("c#") || input.Contains("python") ||
               input.Contains("funzione") || input.Contains("classe") ||
               input.Contains("metodo");
    }

    public static bool IsTheoryQuestion(string input)
    {
        return input.Contains("spiega") || input.Contains("cos'è") ||
               input.Contains("cosa è") || input.Contains("perché") ||
               input.Contains("definizione") || input.Contains("teoria");
    }

    public static string NormalizeForConsole(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        string clean = text
            .Replace("\\(", "")
            .Replace("\\)", "")
            .Replace("\\frac{", "(")
            .Replace("}{", ")/")
            .Replace("}", "")
            .Replace("\\", "");

        while (clean.Contains("  "))
            clean = clean.Replace("  ", " ");

        return clean.Trim();
    }

    public static void LogInteraction(string role, string content)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[LOG {DateTime.Now:HH:mm:ss}] {role.ToUpper()}: {content}");
        Console.ResetColor();
    }
}
