using System.Text.RegularExpressions;
using Spectre.Console;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

var subsetPrefix = new Regex("^[A-Z]{6}\\+", RegexOptions.Compiled);

var inputs = args.Length > 0
    ? args.Select(a => a.Trim().Trim('"')).Where(a => a.Length > 0).ToList()
    : new List<string> { AnsiConsole.Ask<string>("Prevuci PDF ili folder ovde, ili upiši putanju:").Trim().Trim('"') };

var files = new List<string>();
foreach (var i in inputs)
{
    if (Directory.Exists(i))
        files.AddRange(Directory.EnumerateFiles(i, "*.pdf", SearchOption.AllDirectories).OrderBy(f => f));
    else if (File.Exists(i))
        files.Add(i);
    else
        AnsiConsole.MarkupLine($"[red]Putanja ne postoji:[/] {Markup.Escape(i)}");
}
files = files.Distinct().ToList();

if (files.Count == 0)
{
    AnsiConsole.MarkupLine("[red]Nema PDF fajlova za proveru.[/]");
    return;
}

var problemFiles = new List<string>();

foreach (var file in files)
{
    AnsiConsole.Write(new Rule($"[yellow]{Markup.Escape(Path.GetFileName(file))}[/]").LeftJustified());
    try
    {
        List<FontUse> uses;
        using (var doc = PdfDocument.Open(file))
            uses = FontScanner.Scan(doc);

        if (uses.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Nema fontova u ovom fajlu.[/]");
            continue;
        }

        var groups = uses
            .GroupBy(u => subsetPrefix.IsMatch(u.BaseFont) ? u.BaseFont.Substring(7) : u.BaseFont)
            .OrderBy(g => g.Key)
            .ToList();

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Font");
        table.AddColumn("Tip");
        table.AddColumn("Embedovan");
        table.AddColumn("Subset");
        table.AddColumn("Strane");
		table.AddColumn("Neugrađen na stranama");

        var missing = new List<string>();

        foreach (var g in groups)
        {
            bool isSubset = g.Any(u => subsetPrefix.IsMatch(u.BaseFont));
            var pages = g.Select(u => u.Page).Distinct().OrderBy(p => p).ToList();
            var type = g.First().Subtype;

            FontStatus status =
                g.Any(u => u.Status == FontStatus.NotEmbedded) ? FontStatus.NotEmbedded :
                g.Any(u => u.Status == FontStatus.Type3) ? FontStatus.Type3 :
                FontStatus.Embedded;

            if (status == FontStatus.NotEmbedded) missing.Add(g.Key);

            var statusText = status switch
            {
                FontStatus.Embedded => "[green]DA[/]",
                FontStatus.NotEmbedded => "[bold red]NE[/]",
                _ => "[yellow]Type3[/]"
            };

            var pageText = string.Join(", ", pages.Take(10))
                           + (pages.Count > 10 ? $" … (+{pages.Count - 10})" : "");

            var missingPages = g.Where(u => u.Status == FontStatus.NotEmbedded)
                                .Select(u => u.Page).Distinct().OrderBy(p => p).ToList();
            var missingText = missingPages.Count == 0
                ? "[grey]-[/]"
                : "[red]" + string.Join(", ", missingPages.Take(10))
                  + (missingPages.Count > 10 ? $" … (+{missingPages.Count - 10})" : "") + "[/]";

            table.AddRow(Markup.Escape(g.Key), Markup.Escape(type), statusText,
                         isSubset ? "da" : "[grey]ne[/]", pageText, missingText);
        }

        AnsiConsole.Write(table);

        if (missing.Count > 0)
        {
            problemFiles.Add(Path.GetFileName(file));
            AnsiConsole.MarkupLine(
                $"[bold red]Pažnja:[/] {missing.Count} font(a) nije ugrađeno: {Markup.Escape(string.Join(", ", missing))}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Svi fontovi su ugrađeni[/] (ukupno: {groups.Count}).");
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Ne mogu da pročitam fajl:[/] {Markup.Escape(ex.Message)}");
    }
}

if (files.Count > 1)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine(problemFiles.Count == 0
        ? $"[green]Provereno fajlova: {files.Count}. Nema problema.[/]"
        : $"[bold red]Fajlovi sa neugrađenim fontovima: {problemFiles.Count} od {files.Count}[/]");
}

if (!Console.IsInputRedirected)
{
    AnsiConsole.MarkupLine("\n[grey]Pritisni bilo koji taster za izlaz...[/]");
    Console.ReadKey(true);
}

enum FontStatus { Embedded, NotEmbedded, Type3 }

record FontUse(int Page, string BaseFont, string Subtype, FontStatus Status);

static class FontScanner
{
    public static List<FontUse> Scan(PdfDocument doc)
    {
        var result = new List<FontUse>();

        IToken? Resolve(IToken? t)
        {
            for (int i = 0; i < 20 && t != null; i++)
            {
                if (t is IndirectReferenceToken r) t = doc.Structure.GetObject(r.Data)?.Data;
                else if (t is ObjectToken o) t = o.Data;
                else return t;
            }
            return t;
        }

        DictionaryToken? GetDict(DictionaryToken d, string key) =>
            d.Data.TryGetValue(key, out var v) ? Resolve(v) as DictionaryToken : null;

        string? GetName(DictionaryToken d, string key) =>
            d.Data.TryGetValue(key, out var v) && Resolve(v) is NameToken n ? n.Data : null;

        foreach (var page in doc.GetPages())
        {
            var resources = page.Dictionary.Data.TryGetValue("Resources", out var rt)
                ? Resolve(rt) as DictionaryToken : null;
            var fonts = resources != null ? GetDict(resources, "Font") : null;
            if (fonts == null) continue;

            foreach (var (_, fontRef) in fonts.Data)
            {
                if (Resolve(fontRef) is not DictionaryToken font) continue;

                var baseFont = GetName(font, "BaseFont") ?? "(bez imena)";
                var subtype = GetName(font, "Subtype") ?? "?";
                var owner = font;

                if (subtype == "Type0" &&
                    font.Data.TryGetValue("DescendantFonts", out var df) &&
                    Resolve(df) is ArrayToken arr && arr.Data.Count > 0 &&
                    Resolve(arr.Data[0]) is DictionaryToken cid)
                {
                    owner = cid;
                }

                var descriptor = GetDict(owner, "FontDescriptor");

                FontStatus status;
                if (subtype == "Type3") status = FontStatus.Type3;
                else if (descriptor != null &&
                         (descriptor.Data.ContainsKey("FontFile") ||
                          descriptor.Data.ContainsKey("FontFile2") ||
                          descriptor.Data.ContainsKey("FontFile3")))
                    status = FontStatus.Embedded;
                else status = FontStatus.NotEmbedded;

                result.Add(new FontUse(page.Number, baseFont, subtype, status));
            }
        }

        return result;
    }
}