using System.Text.RegularExpressions;

namespace Ritmo.Core.Text;

/// <summary>Tipo de bloque de un Markdown sencillo.</summary>
public enum MdBlockKind { Paragraph, Heading, Bullet, Task, Numbered }

/// <summary>Un fragmento de texto con estilo dentro de un bloque.</summary>
public sealed record MdInline(string Text, bool Bold = false, bool Italic = false, bool Code = false, string? Href = null);

/// <summary>
/// Un bloque. <paramref name="Level"/> = nivel del encabezado (1-3) o el ordinal
/// de una lista numerada. <paramref name="Checked"/> = estado de una casilla de tarea.
/// </summary>
public sealed record MdBlock(MdBlockKind Kind, IReadOnlyList<MdInline> Inlines, int Level = 0, bool Checked = false);

/// <summary>
/// Parser de un subconjunto de Markdown, PURO y testable (sin UI). El host
/// (Ritmo.App) convierte los bloques a un RichTextBlock. Soporta:
/// encabezados (#, ##, ###), viñetas (-, *), listas numeradas (1. / 1)),
/// casillas de tarea ([ ] / [x]), <b>**negrita**</b>, <i>*cursiva*</i>
/// o _cursiva_, `código` y enlaces [texto](url). Suficiente para notas.
/// </summary>
public static partial class MarkdownLite
{
    /// <summary>Convierte un texto Markdown en una lista de bloques.</summary>
    public static IReadOnlyList<MdBlock> Parse(string? markdown)
    {
        var blocks = new List<MdBlock>();
        if (string.IsNullOrEmpty(markdown)) return blocks;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) continue;   // las líneas en blanco solo separan

            if (line.StartsWith("### ")) { blocks.Add(new MdBlock(MdBlockKind.Heading, ParseInlines(line[4..]), 3)); continue; }
            if (line.StartsWith("## ")) { blocks.Add(new MdBlock(MdBlockKind.Heading, ParseInlines(line[3..]), 2)); continue; }
            if (line.StartsWith("# ")) { blocks.Add(new MdBlock(MdBlockKind.Heading, ParseInlines(line[2..]), 1)); continue; }

            var t = line.TrimStart();

            // Casilla de tarea: "- [ ]", "- [x]", "[ ]", "[x]" o "[]" (con o sin guion).
            var task = TaskRegex().Match(t);
            if (task.Success)
            {
                bool done = task.Groups[1].Value is "x" or "X";
                blocks.Add(new MdBlock(MdBlockKind.Task, ParseInlines(task.Groups[2].Value), Checked: done));
                continue;
            }

            if (t.StartsWith("- ") || t.StartsWith("* "))
            { blocks.Add(new MdBlock(MdBlockKind.Bullet, ParseInlines(t[2..]))); continue; }

            // Lista numerada: "1. texto" o "1) texto".
            var num = NumberRegex().Match(t);
            if (num.Success)
            {
                int n = int.TryParse(num.Groups[1].Value, out var x) ? x : 0;
                blocks.Add(new MdBlock(MdBlockKind.Numbered, ParseInlines(num.Groups[2].Value), Level: n));
                continue;
            }

            blocks.Add(new MdBlock(MdBlockKind.Paragraph, ParseInlines(line)));
        }
        return blocks;
    }

    // link | bold | code | italic(*) | italic(_)
    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)|\*\*([^*]+)\*\*|`([^`]+)`|\*([^*]+)\*|_([^_]+)_")]
    private static partial Regex TokenRegex();

    // Casilla de tarea: guion opcional, [ ] / [x] / [], y texto tras un espacio (o vacío).
    [GeneratedRegex(@"^(?:[-*]\s+)?\[([ xX]?)\](?:\s+(.+)|\s*)$")]
    private static partial Regex TaskRegex();

    // Lista numerada: "1. texto" o "1) texto".
    [GeneratedRegex(@"^(\d+)[.)]\s+(.+)$")]
    private static partial Regex NumberRegex();

    /// <summary>Parsea los estilos en línea de un texto a una lista de fragmentos.</summary>
    public static IReadOnlyList<MdInline> ParseInlines(string text)
    {
        var inlines = new List<MdInline>();
        if (string.IsNullOrEmpty(text)) return inlines;

        int pos = 0;
        foreach (Match m in TokenRegex().Matches(text))
        {
            if (m.Index > pos)
                inlines.Add(new MdInline(text[pos..m.Index]));

            if (m.Groups[1].Success)            // [texto](url)
                inlines.Add(new MdInline(m.Groups[1].Value, Href: m.Groups[2].Value));
            else if (m.Groups[3].Success)       // **negrita**
                inlines.Add(new MdInline(m.Groups[3].Value, Bold: true));
            else if (m.Groups[4].Success)       // `código`
                inlines.Add(new MdInline(m.Groups[4].Value, Code: true));
            else if (m.Groups[5].Success)       // *cursiva*
                inlines.Add(new MdInline(m.Groups[5].Value, Italic: true));
            else if (m.Groups[6].Success)       // _cursiva_
                inlines.Add(new MdInline(m.Groups[6].Value, Italic: true));

            pos = m.Index + m.Length;
        }
        if (pos < text.Length)
            inlines.Add(new MdInline(text[pos..]));

        return inlines;
    }
}
