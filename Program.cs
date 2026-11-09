using System.Text.RegularExpressions;
using Spectre.Console;
using UglyToad.PdfPig;

var subsetPrefix = new Regex("^[A-Z]{6}\\+", RegexOptions.Compiled);

string input = args.Length > 0
    ? args[0]
    : AnsiConsole.Ask<string>("Prevuci PDF ili folder ovde, ili upiši putanju:");
input = input.Trim().Trim('"');

List<string> files;
if (Directory.Exists(input))
    files = Directory.EnumerateFiles(input, "*.pdf", SearchOption.AllDirectories)
                     .OrderBy(f => f).ToList();
else if (File.Exists(input))
    files = new List<string> { input };
else
{
    AnsiConsole.MarkupLine("[red]Putanja ne postoji.[/]");
    return;
}

foreach (var file in files)
{
    AnsiConsole.Write(new Rule($"[yellow]{Markup.Escape(Path.GetFileName(file))}[/]").LeftJustified());
    try
    {
        var fonts = new SortedDictionary<string, (string Raw, bool Subset, SortedSet<int> Pages)>();

        using (var doc = PdfDocument.Open(file))
        {
            foreach (var page in doc.GetPages())
            {
                foreach (var letter in page.Letters)
                {
                    var raw = letter.FontName ?? "(nepoznat)";
                    bool isSubset = subsetPrefix.IsMatch(raw);
                    var name = isSubset ? raw.Substring(7) : raw;

                    if (!fonts.TryGetValue(name, out var info))
                    {
                        info = (raw, isSubset, new SortedSet<int>());
                        fonts[name] = info;
                    }
                    info.Pages.Add(page.Number);
                }
            }
        }

        if (fonts.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Nema teksta sa fontovima (možda su slova pretvorena u krivine).[/]");
            continue;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Font");
        table.AddColumn("Subset");
        table.AddColumn("Original");
        table.AddColumn("Strane");

        foreach (var (name, info) in fonts)
        {
            var pages = info.Pages;
            var pageText = string.Join(", ", pages.Take(10))
                           + (pages.Count > 10 ? $" … (+{pages.Count - 10})" : "");
            table.AddRow(Markup.Escape(name),
                         info.Subset ? "[green]da[/]" : "[grey]ne[/]",
                         Markup.Escape(info.Raw),
                         pageText);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"Ukupno fontova: [bold]{fonts.Count}[/]");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Ne mogu da pročitam fajl:[/] {Markup.Escape(ex.Message)}");
    }
}