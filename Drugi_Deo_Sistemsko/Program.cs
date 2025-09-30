using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DrugiDeo
{
   class Program
   {
      private static readonly HttpClient httpClient = new HttpClient();
      private static readonly ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>();
      private static readonly object cacheLock = new object();
      private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

      static void Main()
      {
         //tcp
         TcpListener server = new TcpListener(IPAddress.Any, 8080);
         server.Start();
         Console.WriteLine("Server started...");

         while (true)
         {
            TcpClient client = server.AcceptTcpClient();
            Console.WriteLine("Client connected...");
            Task.Run(() => HandleClientAsync(client));
         }
      }

      private static async Task HandleClientAsync(TcpClient client)
      {
         //ovo je drugi nacin za oslobadjanje resursa koji je bolji jer sam resava
         using (client)
         using (NetworkStream stream = client.GetStream())
         using (StreamReader reader = new StreamReader(stream))
         {
            try
            {
               //citam prvi red
               string? requestLine = await reader.ReadLineAsync();
               if (string.IsNullOrEmpty(requestLine))
                  return;

               Console.WriteLine("Zahtev: " + requestLine);
               //delim zahtev na delove i ispitujem po tokenima
               string[] tokens = requestLine.Split(' ');

               //da li je zahtev validan = GET
               if (tokens.Length < 2 || tokens[0] != "GET")
               {
                  await SendResposneAsync(stream, "<h2> No favicon</h2>", "text/html", "200 OK");
                  return;
               }
               //putanja iz zahteva - /search.json?title=Harry
               string path = tokens[1];
               if (path == "/favicon.ico")
               {
                  await SendResposneAsync(stream, "<h2>No favicon</h2>", "text/html", "200 OK");
                  return;
               }

               string responseContent;
               //log za kes
               if (cache.TryGetValue(path, out responseContent))
               {
                  Console.WriteLine("Cashe HIT: " + path);
               }
               else
               {
                  Console.WriteLine("Cashe miss: " + path);
                  if (path.StartsWith("/search.json?"))
                     responseContent = await FetchSearchResults(path);
                  else
                     responseContent = await FetchFromOpenLibrary(path);
                  cache[path] = responseContent;
               }
               await SendResposneAsync(stream, responseContent, "text/html", "200 OK");

            }
            catch (Exception ex)
            {
               Console.WriteLine("Greska: " + ex.Message);
            }
         }


      }

      private static async Task<string?> FetchFromOpenLibrary(string query)
      {
         try
         {
            string apiUrl = "https://openlibrary.org" + query;
            string json = await httpClient.GetStringAsync(apiUrl);

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
               var root = doc.RootElement;

               if (root.TryGetProperty("docs", out JsonElement booksArray) && booksArray.ValueKind == JsonValueKind.Array)
               {
                  var htmlBuilder = new StringBuilder();
                  htmlBuilder.Append("<html><body>");
                  htmlBuilder.Append("<h2>Rezultati pretrage</h2>");
                  htmlBuilder.Append("<table border='1' cellpaddingc='8' cellspacing='0' style='border-collapse:collapse;'>");
                  htmlBuilder.Append("<tr><th>Naslov</th><th>Autor</th><th>Godina izdavanja</th>");

                  int count = 0;
                 
                  foreach (var item in booksArray.EnumerateArray())
                  {
                     string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                     string author = "";
                     if (item.TryGetProperty("author_name", out var authors) && authors.ValueKind == JsonValueKind.Array && authors.GetArrayLength() > 0)
                        author = authors[0].GetString() ?? "";
                     string year = item.TryGetProperty("first_published_year", out var y) ? y.GetInt32().ToString() : "";

                     htmlBuilder.Append("<tr>");
                     htmlBuilder.Append($"<td>{WebUtility.HtmlEncode(title)}</td>");
                     htmlBuilder.Append($"<td>{WebUtility.HtmlEncode(author)}</td>");
                     htmlBuilder.Append($"<td>{WebUtility.HtmlEncode(year)}</td>");
                     htmlBuilder.Append("</tr>");

                     if (++count >= 50) break; //ogranicavam na 50
                  }

                  htmlBuilder.Append("</table>");
                  htmlBuilder.Append("</body></html>");
                  return htmlBuilder.ToString();
               }
               else
                  return $"<html><body><pre>{WebUtility.HtmlEncode(json)}</pre></body></html>";
            }

         }
         catch (Exception ex)
         {
            return $"<html><body><h2>Greska pri preuzimanju sa OpenLibrary API-ja: </h2><pre>{WebUtility.HtmlEncode(ex.Message)}</pre><>/body</html>";
         }
      }

      private static async Task<string> FetchSearchResults(string path)
      {
         try
         {
            // dekodiramo URL
            string query = WebUtility.UrlDecode(path);

            // provera da li sadrzi znak "?" (znači da ima query parametre)
            int queryIndex = query.IndexOf('?');
            if (queryIndex == -1)
               return "<html><body><h2>400 Bad Request - query parametri nedostaju</h2></body></html>";

            // izdvajamo deo posle ?, npr "author=tolkien&sort=new"
            string queryParams = query.Substring(queryIndex);

            // pravimo URL za OpenLibrary
            string apiUrl = "https://openlibrary.org/search.json" + queryParams;

            // saljemo zahtev
            string json = await httpClient.GetStringAsync(apiUrl);

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
               var root = doc.RootElement;
               if (root.TryGetProperty("docs", out JsonElement docsArray) && docsArray.ValueKind == JsonValueKind.Array)
               {
                  if (docsArray.GetArrayLength() == 0)
                     return "<html><body><h2>Nema rezultata</h2></body></html>";

                  var htmlBuilder = new StringBuilder();
                  htmlBuilder.Append("<html><body><h2>Rezultati pretrage:</h2>");
                  htmlBuilder.Append("<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;'>");
                  htmlBuilder.Append("<tr><th>Naslov</th><th>Autor</th><th>Godina</th></tr>");

                  int count = 0;
                  foreach (var item in docsArray.EnumerateArray())
                  {
                     string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";

                     string author = "";
                     if (item.TryGetProperty("author_name", out var authors) &&
                         authors.ValueKind == JsonValueKind.Array &&
                         authors.GetArrayLength() > 0)
                        author = authors[0].GetString() ?? "";

                     string year = item.TryGetProperty("first_publish_year", out var y) ? y.ToString() : "";

                     htmlBuilder.Append("<tr>");
                     htmlBuilder.Append($"<td>{WebUtility.HtmlEncode(title)}</td>");
                     htmlBuilder.Append($"<td>{WebUtility.HtmlEncode(author)}</td>");
                     htmlBuilder.Append($"<td>{WebUtility.HtmlEncode(year)}</td>");
                     htmlBuilder.Append("</tr>");

                     if (++count >= 50) break;
                  }

                  htmlBuilder.Append("</table></body></html>");
                  return htmlBuilder.ToString();
               }
               else
               {
                  return $"<html><body><pre>{WebUtility.HtmlEncode(json)}</pre></body></html>";
               }
            }
         }
         catch (Exception ex)
         {
            return $"<html><body><h2>Greška pri pretrazi:</h2><pre>{WebUtility.HtmlEncode(ex.Message)}</pre></body></html>";
         }
      }

      private static async Task SendResposneAsync(NetworkStream stream, string content, string contentType, string status = "200 OK")
      {
         string response =
            $"HTTP/1.1 {status}\r\n" +
            $"Content-Type: {contentType}; charset=UTF-8\r\n" +
            $"Content-Length: {Encoding.UTF8.GetByteCount(content)}\r\n" +
            "Connection: close\r\n" +
            "\r\n" +
            content;
         byte[] buffer = Encoding.UTF8.GetBytes(response);

         await stream.WriteAsync(buffer, 0, buffer.Length);
      }
   }
}