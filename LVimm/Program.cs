using System;
using System.Net.Http;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Drawing;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Net;

public class Game
{
    public string Name { get; set; }

    public string System { get; set; }
    public string Region { get; set; }
    public string Version { get; set; }
    public string Lng { get; set; }
    public string Rating { get; set; }
    public long size { get; set; }
    public int Id { get; set; }
    public string IId { get; set; }
    public string mediaId { get; set; }
    public string alt { get; set; }
}

class Program
{
    static int options = 1; // options that are not consoles like settings
    static string gamePath = "";
    static long totalSize = 0;
    static long estimatedExtraSize = 0;
    static int gameIndex = 0;
    static int downloadIndexSize = 0;
    static int downloadIndex = 0;
    static List<long> gameSizes = new List<long>();
    static List<Game> games = new List<Game>();
    static async Task Main(string[] args)
    {
        loadSettings();
        await displayConsolesMenu();
    }

    static void loadSettings()
    {
        string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string lvimmFilePath = Path.Combine(exeDirectory, "data.lvimm");
       

        if (File.Exists(lvimmFilePath))
        {
            try
            {
                // Load the XML file
                XElement gameSettings = XElement.Load(lvimmFilePath);
                XElement gamePathElement = gameSettings.Element("GamePath");

                if (gamePathElement != null)
                {
                    gamePath = gamePathElement.Value;
                    Console.WriteLine($"Game Path Loaded: {gamePath}");
                }
                else
                {
                    Console.WriteLine("Game Path not set in the configuration file.");
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Error loading settings: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Settings file not found. Creating a new one.");
            Console.WriteLine("Insert Game Path:");
            Console.Write("Path >> ");
            gamePath = Console.ReadLine();

            if (Directory.Exists(gamePath))
            {
                // Create the XML file with the new game path
                XElement gameSettings = new XElement("GameSettings",
                    new XElement("GamePath", gamePath)
                );

                try
                {
                    // Save the XML to the file
                    gameSettings.Save(lvimmFilePath);
                    Console.WriteLine($"Successfully Set Game Path to: {gamePath}");
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"Error saving settings: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("The specified path does not exist. Please try again.");
            }
        }
    }
    static bool CheckValidOption(string a, int l)
    {
        try
        {
            int p = int.Parse(a);
            return p >= 0 && p <= l;
        }
        catch
        {
            return false;
        }
    }

    static void populateFolders(string gamePath, List<(string Name, string Year)> consoles)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            Console.WriteLine("Invalid or nonexistent game path.");
            return;
        }

        foreach (var console in consoles)
        {
            string consoleFolderPath = Path.Combine(gamePath, console.Name);

            try
            {
                if (!Directory.Exists(consoleFolderPath))
                {
                    Directory.CreateDirectory(consoleFolderPath);
                    
                }
                
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Error creating folder for console '{console.Name}': {ex.Message}");
            }
        }
    }
    static async Task displayConsolesMenu()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("  LGame Downloader | Consoles Available  ");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("         Access Settings : [0]           ");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("Select an option by typing the index in []");
        Console.WriteLine("-----------------------------------------");

        string url = "https://vimm.net/vault";
        var consoles = new List<(string Name, string Year, string Link)>();
        var httpClient = new HttpClient();

        try
        {
            var htmlContent = await httpClient.GetStringAsync(url);
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(htmlContent);

            var tableRows = htmlDocument.DocumentNode.SelectNodes("//table[@class='rounded']//tr");

            if (tableRows != null)
            {
                foreach (var row in tableRows)
                {
                    var columns = row.SelectNodes("td");
                    if (columns != null && columns.Count > 1)
                    {
                        var nameNode = columns[0].SelectSingleNode("a");
                        var yearNode = columns[1].SelectSingleNode(".//span");

                        if (nameNode != null && yearNode != null)
                        {
                            string name = nameNode.InnerText.Trim();
                            string year = yearNode.InnerText.Trim();
                            string link = nameNode.GetAttributeValue("href", string.Empty);

                            consoles.Add((Name: name, Year: year, Link: link));
                        }
                    }
                }
            }

            Console.WriteLine("Available Consoles:");
            foreach (var console in consoles)
            {
                Console.WriteLine($"[{consoles.IndexOf(console) + 1}] {console.Name} - {console.Year}");
            }
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Error fetching or parsing HTML: {ex.Message}");
        }

        Console.Write("Option >> ");
        string answer = Console.ReadLine();
        if (CheckValidOption(answer, consoles.Count))
        {
            int answerParsed = int.Parse(answer);
            if (answerParsed == 0)
            {
                await displaySettingsMenu();
            }
            else
            {
                var selectedConsole = consoles[answerParsed - 1];
                await displayConsoleMenu(selectedConsole.Name, selectedConsole.Link);
            }
        }
        else
        {
            Console.WriteLine("Invalid option selected.");
        }
    }

    static async Task displayConsoleMenu(string consoleName, string consoleLink)
    {
        
        Console.Clear();
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine($"LGame Downloader | {consoleName} Games");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine($"Current Game Folder: {gamePath}");
        Console.WriteLine("[0] Back to Consoles Menu");
        Console.WriteLine($"Searching data for {consoleName}...Games Found: [x{gameIndex}] ");
        gameIndex = 0;
        totalSize = 0;
        estimatedExtraSize = 0;
        gameSizes.Clear();
        games.Clear();
        

        // Construct the URL correctly
        string url = $"https://vimm.net{consoleLink}";
        var httpClient = new HttpClient();

        try
        {
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"HTTP Request Error: {response.ReasonPhrase} url used: {url}");
                return;
            }

            var htmlContent = await response.Content.ReadAsStringAsync();
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(htmlContent);

            // Locate menu links
            var menuLinks = htmlDocument.DocumentNode.SelectNodes("//div[@id='vaultMenu']//a");
            if (menuLinks != null)
            {
                var tasks = new List<Task>();
                foreach (var link in menuLinks)
                {
                    string href = link.GetAttributeValue("href", string.Empty);
                    if (!string.IsNullOrEmpty(href) && href.StartsWith("/vault/"))
                    {
                        string itemLink = $"https://vimm.net{href}";

                        // Create and add the task to the list
                        tasks.Add(ScrapeAndPrintGameDetails(httpClient, itemLink, consoleName));
                        
                    }
                }

                // Run all tasks concurrently
                await Task.WhenAll(tasks);
                var sortedGames = games.OrderBy(game => game.Name);
                Console.Clear();
                Console.WriteLine("-----------------------------------------");
                Console.WriteLine($"LGame Downloader | {consoleName} Games");
                Console.WriteLine("-----------------------------------------");
                Console.WriteLine($"Current Game Folder: {gamePath}");
                Console.WriteLine("[0] Back to Consoles Menu");
                Console.WriteLine();
                Console.WriteLine($"Games Found: [{games.Count}]");
                Console.WriteLine();
                foreach (var game in sortedGames)
                {
                    Console.WriteLine($"[{game.Id}] {game.Name} | {game.Region} | {game.Version} | {game.Lng} | {game.Rating} | Size: {ParseBytesToSize(game.size)}");
                }
            }
            else
            {
                Console.WriteLine("Menu links not found on the page.");
            }
        }
        catch (HttpRequestException httpEx)
        {
            Console.WriteLine($"HTTP Request Exception: {httpEx.Message}");
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Error fetching or parsing HTML: {ex.Message} url used: {url}");
        }
        estimatedExtraSize = (long)Queryable.Average(gameSizes.AsQueryable()) * gameIndex;
        Console.WriteLine($"Total Size : {ParseBytesToSize(totalSize)} + {ParseBytesToSize(estimatedExtraSize)} : {ParseBytesToSize(totalSize+estimatedExtraSize)}");
        Console.Write("Option >> ");
        string answer = Console.ReadLine();
        var selectedIds = answer.Split(',').Select(id => id.Trim()).ToList();
        downloadIndexSize = selectedIds.Count;
        downloadIndex = 0;
        foreach (var id in selectedIds)
        {
            if (int.TryParse(id, out int gameId))
            {
                var game = games.FirstOrDefault(g => g.Id == gameId);
                if (game != null)
                {
                    downloadIndex++;
                    await StartDownload(game);
                }
                else
                {
                    Console.WriteLine($"Game with ID {gameId} not found.");
                }
            }
            else
            {
                Console.WriteLine($"Invalid option: {id}");
            }
        }
        Console.Clear();
        await displayConsolesMenu();
    }

    static async Task StartDownload(Game game)
    {
        var downloadUrl = $"https://download2.vimm.net/?mediaId={game.mediaId}";

        var httpClientHandler = new HttpClientHandler();
        var httpClient = new HttpClient(httpClientHandler);
        

        // Set headers to mimic a browser request
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-GB,en-US;q=0.9,en;q=0.8");
        httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        httpClient.DefaultRequestHeaders.Referrer = new Uri("https://vimm.net/vault/84315");
        httpClient.DefaultRequestHeaders.Add("Origin", "https://vimm.net");

      

        Console.Clear();
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine($"     LGame Downloader | Game [{downloadIndex}/{downloadIndexSize}]    ");
        Console.WriteLine("-----------------------------------------");

        try
        {
            // Send the GET request to get the file
            using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                if (response.IsSuccessStatusCode)
                {
                    var contentDisposition = response.Content.Headers.ContentDisposition;
                    var fileName = contentDisposition?.FileName?.Trim('"') ?? $"{game.Name}.zip";
                    var filePath = Path.Combine(gamePath, fileName);

                    if (File.Exists(filePath))
                    {
                        Console.WriteLine($"File '{fileName}' already exists. Skipping download.");
                        return;
                    }

                    long totalBytes = response.Content.Headers.ContentLength.HasValue ? response.Content.Headers.ContentLength.Value : -1L;
                    var buffer = new byte[8192];
                    long totalBytesRead = 0;
                    int bytesRead;

                    Console.WriteLine($"Game: {game.Name}");
                    Console.WriteLine($"Speed: Calculating...");
                    Console.WriteLine($"Downloaded: 0 / {ParseBytesToSize(totalBytes)}");

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true))
                    {
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            stopwatch.Stop();
                            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                            var speed = (totalBytesRead / (1024.0 * 1024.0)) / elapsedSeconds; // Speed in MB/s

                            Console.Clear();
                            Console.WriteLine("-----------------------------------------");
                            Console.WriteLine($"     LGame Downloader | Game [{downloadIndex}/{downloadIndexSize}]    ");
                            Console.WriteLine("-----------------------------------------");
                            Console.WriteLine($"Game: {game.Name}");
                            Console.WriteLine($"Speed: {speed:0.00} MB/s");
                            Console.WriteLine($"Downloaded: {ParseBytesToSize(totalBytesRead)} / {ParseBytesToSize(totalBytes)}");

                            stopwatch.Start(); // Restart stopwatch for next interval
                        }

                        stopwatch.Stop();
                    }

                    Console.WriteLine($"Download completed for {fileName}. Saving to {filePath}");

                    // Navigate to appropriate folder
                    string consoleFolderPath = Path.Combine(gamePath, game.System);
                    if (!Directory.Exists(consoleFolderPath))
                    {
                        Directory.CreateDirectory(consoleFolderPath);
                    }
                    if (File.Exists(Path.Combine(consoleFolderPath, fileName)))
                    {
                        Console.WriteLine($"File '{fileName}' already exists.");
                        File.Delete(filePath);
                        return;
                    } else
                    {
                        File.Move(filePath, Path.Combine(consoleFolderPath, fileName));
                    }
                    
                }
                else
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        Console.WriteLine("Download unavailable: The requested file could not be found.");
                    }
                    else
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Download failed: {response.ReasonPhrase}");
                        Console.WriteLine($"Response Content: {responseContent}");
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Request Exception: {ex.Message}");
        }
    }

    static async Task ScrapeAndPrintGameDetails(HttpClient httpClient, string itemLink, string consoleName)
    {
        
        try
        {
            var itemResponse = await httpClient.GetAsync(itemLink);
            if (!itemResponse.IsSuccessStatusCode)
            {
                //Console.WriteLine($"HTTP Request Error: {itemResponse.ReasonPhrase} url used: {itemLink}");
                return;
            }

            var itemHtmlContent = await itemResponse.Content.ReadAsStringAsync();
            var itemHtmlDocument = new HtmlDocument();
            itemHtmlDocument.LoadHtml(itemHtmlContent);

            var itemRows = itemHtmlDocument.DocumentNode.SelectNodes("//tr");
            if (itemRows != null)
            {
                foreach (var row in itemRows)
                {
                    var cells = row.SelectNodes("td");
                    if (cells != null && cells.Count >= 5)
                    {
                        string href = cells[0].SelectSingleNode("a")?.GetAttributeValue("href", string.Empty) ?? string.Empty;
                        string title = cells[0].SelectSingleNode("a")?.InnerText.Trim() ?? string.Empty;
                        string region = cells[1].SelectSingleNode("img")?.GetAttributeValue("title", string.Empty) ?? string.Empty;
                        string version = cells[2].InnerText.Trim();
                        string languages = cells[3].InnerText.Trim();
                        string rating = cells[4].SelectSingleNode("a")?.InnerText.Trim() ?? string.Empty;

                        // Scrape size information from the detailed page
                       
                        
                        var (size2, mediaId, alt) = await GetSizeAndIdsFromDetailedPage(httpClient, $"https://vimm.net{href}");
                        long size = size2;

                        // Print the game details immediately
                        totalSize += size;
                        gameIndex++;
                        games.Add(new Game
                        {
                            Name = title,
                            Region = region,
                            Version = version,
                            Lng = languages,
                            Rating = rating,
                            System = consoleName,
                            Id = gameIndex,
                            mediaId = mediaId,
                            size = size,
                            alt = alt,
                            IId = href
                        });
                        string NullSize = "Not Detected";
                        string fSize = size != 0 ? ParseBytesToSize(size) : NullSize;
                        if (size != 0)
                        {
                            gameSizes.Add(size);
                        }
                        Console.Clear();
                        Console.WriteLine("-----------------------------------------");
                        Console.WriteLine($"LGame Downloader | {consoleName} Games");
                        Console.WriteLine("-----------------------------------------");
                        Console.WriteLine($"Current Game Folder: {gamePath}");
                        Console.WriteLine("[0] Back to Consoles Menu");
                        Console.WriteLine($"Searching data for {consoleName}...Games Found: [x{gameIndex}] ");
                        //Console.WriteLine($"[{gameIndex}] {title} | {region} | {version} | {languages} | {rating} | Size: {fSize}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ////Console.WriteLine($"Error fetching or parsing game details: {ex.Message} url used: {itemLink}");
        }
        
    }

    static async Task<(long Size, string MediaId, string Alt)> GetSizeAndIdsFromDetailedPage(HttpClient httpClient, string itemLink)
    {
        try
        {
            var itemResponse = await httpClient.GetAsync(itemLink);
            if (!itemResponse.IsSuccessStatusCode)
            {
                return (0, string.Empty, string.Empty);
            }

            var itemHtmlContent = await itemResponse.Content.ReadAsStringAsync();
            var itemHtmlDocument = new HtmlDocument();
            itemHtmlDocument.LoadHtml(itemHtmlContent);

            // Locate size information
            var sizeNode = itemHtmlDocument.DocumentNode.SelectSingleNode("//td[@id='dl_size']");
            long size = 0;
            if (sizeNode != null)
            {
                string sizeText = sizeNode.InnerText.Trim();
                size = ParseSizeToBytes(sizeText);
            }

            // Locate mediaId and alt values
            var mediaIdNode = itemHtmlDocument.DocumentNode.SelectSingleNode("//input[@name='mediaId']");
            var altNode = itemHtmlDocument.DocumentNode.SelectSingleNode("//input[@name='alt']");

            string mediaId = mediaIdNode?.GetAttributeValue("value", string.Empty) ?? string.Empty;
            string alt = altNode?.GetAttributeValue("value", string.Empty) ?? string.Empty;

            return (size, mediaId, alt);
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Error fetching or parsing details: {ex.Message} url used: {itemLink}");
            return (0, string.Empty, string.Empty);
        }
    }


    static long ParseSizeToBytes(string sizeText)
    {
        sizeText = sizeText.ToUpper().Replace(" ", string.Empty);
        long size = 0;
        if (sizeText.EndsWith("KB"))
        {
            size = long.Parse(sizeText.Replace("KB", string.Empty)) * 1000;
        }
        else if (sizeText.EndsWith("MB"))
        {
            size = long.Parse(sizeText.Replace("MB", string.Empty)) * 1000000;
        }
        else if (sizeText.EndsWith("GB"))
        {
            size = long.Parse(sizeText.Replace("GB", string.Empty)) * 1000000000;
        }
        else if (sizeText.EndsWith("B"))
        {
            size = long.Parse(sizeText.Replace("B", string.Empty));
        }
        return size;
    }

    static string ParseBytesToSize(long size)
    {
        if (size >= 1000000000000)
        {
            return $"{size / 1000000000000.0:F2} TB";
        }
        else if (size >= 1000000000)
        {
            return $"{size / 1000000000.0:F2} GB";
        }
        else if (size >= 1000000)
        {
            return $"{size / 1000000.0:F2} MB";
        }
        else if (size >= 1000)
        {
            return $"{size / 1000.0:F2} KB";
        }
        else
        {
            return $"{size} B";
        }
    }


    static async Task displayGameFolderSetMenu()
    {
        Console.Clear();
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("     LGame Downloader | Quick Install    ");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine($"Current Game Folder: {gamePath}");
        Console.WriteLine("[0] Back to Settings Menu");
        Console.WriteLine("[1] Change Game Folder Path");

        Console.Write("Option >> ");
        string answer = Console.ReadLine();
        if (CheckValidOption(answer, 1)) // Check if the input is valid
        {
            int answerParsed = int.Parse(answer);
            if (answerParsed == 0)
            {
                await displaySettingsMenu(); // Add await for async call
            }
            else if (answerParsed == 1)
            {
                Console.Clear();
                Console.WriteLine("-----------------------------------------");
                Console.WriteLine("     LGame Downloader | Quick Install    ");
                Console.WriteLine("-----------------------------------------");
                Console.WriteLine("Insert New Game Path:");
                Console.Write("Path >> ");
                string gamePath = Console.ReadLine();

                if (Directory.Exists(gamePath))
                {
                    // Define the path for the data.lvimm file
                    string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string lvimmFilePath = Path.Combine(exeDirectory, "data.lvimm");

                    // Create or update the XML file with the new game path
                    XElement gameSettings = new XElement("GameSettings",
                        new XElement("GamePath", gamePath)
                    );

                    // Save the XML to the file
                    gameSettings.Save(lvimmFilePath);

                    Console.WriteLine($"Successfully Set Game Path to: {gamePath}");
                }
                else
                {
                    Console.WriteLine("The specified path does not exist. Please try again.");
                }
            }
        }
        else
        {
            Console.WriteLine("Invalid option selected.");
        }
    }
    static async Task displaySettingsMenu()
    {
        Console.Clear();
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("     LGame Downloader | Quick Install    ");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("Settings Menu:");
        Console.WriteLine("[0] Back to Main Menu");
        Console.WriteLine("[1] Set Games Folder");


        Console.Write("Option >> ");
        string answer = Console.ReadLine();
        if (CheckValidOption(answer, 1))
        {
            int answerParsed = int.Parse(answer);
            if (answerParsed == 0)
            {
                await displayConsolesMenu(); // Add await for async call
            }
            if (answerParsed == 1) 
            {
                await displayGameFolderSetMenu();
            }
        }
        else
        {
            Console.WriteLine("Invalid option selected.");
        }
    }
}
