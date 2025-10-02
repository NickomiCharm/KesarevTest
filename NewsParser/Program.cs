using System.Text.Json;
using HtmlAgilityPack;

string inputFile = args is { Length: > 0 } ? args[0] : PromptFile();

if (!File.Exists(inputFile))
    throw new FileNotFoundException($"Файл не найден: {inputFile}");

static string PromptFile()
{
    Console.Write("Укажите путь к html-файлу: ");
    return Console.ReadLine() ?? throw new InvalidOperationException("Файл не указан");
}

string jsonName = "clean-news.json";
string logName = "log.txt";

File.Delete(logName);
await using BackgroundLogger logger = new BackgroundLogger("log.txt");

string html = await File.ReadAllTextAsync(inputFile);
HtmlDocument doc = new HtmlDocument { OptionFixNestedTags = true };
doc.LoadHtml(html);

NewsParser parser = new NewsParser(logger);
List<NewsItem> news = parser.ParseDocument(doc);

JsonSerializerOptions options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true };
await File.WriteAllTextAsync(jsonName, JsonSerializer.Serialize(news, options));

Console.WriteLine($"Готово. Найдено корректных новостей: {news.Count}");
Console.WriteLine($"Результат: {jsonName}");
Console.WriteLine($"Логи: {logName}");
Console.ReadLine();

return;

