using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

public class NewsParser
{
    private readonly BackgroundLogger _logger;
    private static readonly string Basehref = "https://brokennews.net";

    public NewsParser(BackgroundLogger logger)
    {
        _logger = logger;
    }

    // Парсинг HtmlDocument
    public List<NewsItem> ParseDocument(HtmlDocument document)
    {
        List<NewsItem> results = new List<NewsItem>();

        // Коллекция элементов <li> для поиска новостей
        List<HtmlNode> nodes =
        [
            .. document.DocumentNode.SelectNodes("//li[contains(@class,'news-item')]") ?? Enumerable.Empty<HtmlNode>(),
        ];

        if (!nodes.Any())
        {
            nodes =
            [
                .. document.DocumentNode.SelectNodes("//ul//li") ?? Enumerable.Empty<HtmlNode>()
            ];
        }

        // Делегат для определения мусорного контента
        Func<HtmlNode, bool> isTrash = n =>
        {
            HtmlNodeCollection childs = n.ChildNodes;
            var divs = childs.Where(n => n.Name != "#text");
            bool isNewsBody = divs.Any(n => n.GetAttributeValue("class", "") == "news-body");

            var inner = n.InnerText ?? "";
            if (string.IsNullOrWhiteSpace(CleanText(inner)))
                return true;

            string formattedText = string.Join(", ", divs.Select(d => $"<{d.Name} class=\"{d.GetAttributeValue("class", "")}\">"));
            if (!isNewsBody)
                _logger.Log($"Пропущен блок {n.XPath} (мусорный контент): элементы - {formattedText}.");
            return !isNewsBody;
        };

        // Перебор элементов <li>
        foreach (var node in nodes)
        {
            try
            {
                // Пропуск, если мусорный контент
                if (isTrash(node)) continue;

                // Поиск элементов с требуемыми критериями. Всё, что не подходит - в логи.
                HtmlNode a = node.SelectSingleNode(".//a[@href]");
                string href = a?.GetAttributeValue("href", "")!;
                if (string.IsNullOrWhiteSpace(href) || !href.StartsWith("/"))
                {
                    _logger.Log($"Пропущен блок {node.XPath}: отсутствует ссылка.");
                    continue;
                }

                HtmlNode titleNode = node.SelectSingleNode(".//h4") ?? a!;
                string rawTitle = titleNode?.InnerText!;
                if (string.IsNullOrWhiteSpace(rawTitle))
                {
                    _logger.Log($"Пропущен блок {node.XPath}: отсутствует заголовок.");
                    continue;
                }

                HtmlNode timeNode = node.SelectSingleNode(".//time[@datetime]");
                string dateStr = timeNode?.GetAttributeValue("datetime", "") ?? CleanText(timeNode?.InnerText);

                if (string.IsNullOrWhiteSpace(dateStr))
                {
                    _logger.Log($"Пропущен блок {node.XPath}: отсутствует дата.");
                    continue;
                }

                // Нормализуем получаемую дату во избежание исключений
                string normalizedDate = NormalizeDate(dateStr)!;
                if (normalizedDate == null)
                {
                    _logger.Log($"Пропущен блок {node.XPath}: нераспознанная дата '{dateStr}'.");
                    continue;
                }
                // Нормализуем ссылки - приводим к правильному виду
                var normalizedhref = NormalizeHref(href);
                var title = CleanText(rawTitle);

                var item = new NewsItem
                {
                    Title = title,
                    Url = normalizedhref,
                    Date = normalizedDate
                };

                results.Add(item);
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка парсинга блока: {ex.Message}");
            }
        }

        return results;
    }

    // Нормализация ссылки
    private static string NormalizeHref(string href)
    {
        href = HttpUtility.HtmlDecode(href).Trim();

        if (string.IsNullOrWhiteSpace(href))
            return href;

        href = Regex.Replace(href, @"\s+", "");

        if (href.StartsWith("/"))
            href = Basehref + href;

        return href;
    }

    // Нормализация даты
    private static string? NormalizeDate(string raw)
    {
        raw = HttpUtility.HtmlDecode(raw).Trim();

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, out var dt))
        {
            return dt.ToString("yyyy-MM-dd");
        }

        return null!;
    }

    // Чистка текста от лишних пробелов и тегов
    public static string CleanText(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        string s = HttpUtility.HtmlDecode(input);

        s = Regex.Replace(s, "<.*?>", " ");
        s = Regex.Replace(s, @"[^\S\r\n]+", " ").Trim();

        return s;
    }
}