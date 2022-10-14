using AngleSharp;
using AngleSharp.Dom;
using System.Text;

List<string> templates = new() { "h1.detail-name", "span.price", "span.old-price" };

var config = Configuration.Default.WithDefaultLoader();
using var context = BrowsingContext.New(config);

const string url = "https://www.toy.ru/catalog/boy_transport/";
const string prefix = "https://www.toy.ru";
const string suffix = "&PAGEN_5=";
const string pagesFilterTemplate = "?filterseccode%5B0%5D=transport";
const string fileHeader = "Название региона," +
                            "Хлебные крошки," +
                            "Название товара," +
                            "Цена," +
                            "Цена старая," +
                            "Наличие товара," +
                            "Ссылки на картинки," +
                            "Ссылка на товар";

string path = CreateCSVFile(fileHeader);
object plug = new();

using var document = await context.OpenAsync(url);

var linkList = GetPagesLinks(document, url + pagesFilterTemplate, suffix);

for (int i = 0; i < linkList.Count; i++)
{
    var link = linkList[i];
    var page = await context.OpenAsync(link);
    var products = GetProductLinksFromPage(page, prefix);

    Parallel.ForEach(products, product =>
    {
        var info = ThreadTask(product, context, templates).Result;
        WriteCSVFile(path, info);
    });
}

static List<string> GetObjectInfo(IDocument document, List<string> templates)
{
    List<string> info = new();

    var region = document.QuerySelector("div[class='col-12 select-city-link'] > a").InnerHtml.Trim();
    info.Add(region);

    var navPath = document.QuerySelector("nav.breadcrumb");
    var breadcrumps = navPath.QuerySelectorAll("span span");

    StringBuilder path = new();
    foreach (var b in breadcrumps)
    {
        path.Append(b.InnerHtml.Trim()).Append('>');
    }

    string lastCrump = document.QuerySelector("span[class='breadcrumb-item active d-none d-block']").InnerHtml;
    path.Append(lastCrump);
    info.Add(path.ToString());

    foreach (var template in templates)
    {
        var elem = document.QuerySelector(template);
        if (elem != null) 
        {
            string elenText = elem.InnerHtml;
            info.Add(elenText);
        }
        else
        {
            info.Add("None");
        }
    }

    var isActive = document.QuerySelector("i.v").Parent.TextContent;
    info.Add(isActive);

    var images = document.QuerySelectorAll("div.card-slider-nav > div > img");
    StringBuilder imgAll = new();
    foreach (var image in images)
    {
       imgAll.Append(image.GetAttribute("src")).Append(';');
    }
    info.Add(imgAll.ToString());

    return info;
}

List<string> GetProductLinksFromPage(IDocument document, string prefix)
{
    List<string> links = new();

    var rawLinks = document.QuerySelectorAll("div[class='h-100 product-card'] a[class='d-block img-link text-center gtm-click']");
    foreach (var link in rawLinks) links.Add(prefix + link.GetAttribute("href"));
    return links;
}

List<string> GetPagesLinks(IDocument main, string template, string page_suffix)
{
    List<string> links = new();

    string next = main.QuerySelector("ul[class='pagination justify-content-between']").Children[^2].TextContent.Trim();
    int pagesCount = int.Parse(next);
    pagesCount++;
    links.Add(template);
    for (int i = 2; i < pagesCount; i++)
    {
        links.Add(template + page_suffix + i);
    }
    return links;
}

string CreateCSVFile(string header)
{
    string path = "../data.csv";
    
    if (!File.Exists(path))
    {
        using StreamWriter writer = File.CreateText(path);
        writer.WriteLine(header);
    }
    else
    {
        File.WriteAllText(path, header);
    }
    return path;
}

void WriteCSVFile(string path, List<string> info)
{
    lock (plug)
    {
        using StreamWriter writer = File.AppendText(path);
        StringBuilder stringBuilder = new();

        info.ForEach(x => stringBuilder.Append(x).Append(','));
        writer.WriteLine(stringBuilder.ToString());
    }
}

static async Task<List<string>> ThreadTask(string productLink, IBrowsingContext context, List<string> template)
{
    var document = await context.OpenAsync(productLink);
    List<string> info = GetObjectInfo(document, template);
    document.Dispose();
    return info;
}
