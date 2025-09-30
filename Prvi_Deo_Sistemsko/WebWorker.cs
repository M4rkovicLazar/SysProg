using System.Net;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;

namespace PrviDeo_OpenLibrary
{
   internal static class WebWorker
   {
      internal delegate void LogDelegate(string message);
      internal static void ProcessRequest(
          TcpClient client,
          StreamReader reader,
          StreamWriter writer,
          ConcurrentDictionary<string, CacheEntry> cache,
          LogDelegate log,
          TimeSpan expiryDuration)
      {
         var requestLine = reader.ReadLine();  //cita prvu liniju
         if (string.IsNullOrEmpty(requestLine))
            return;

         log($"Zahtev: {requestLine}"); // GET /search.json? ... HTTP/1.1

         var tokens = requestLine.Split(' ');

         if (tokens.Length < 2 || tokens[0] != "GET")
         {
            WebServer.WriteResponse(writer, "400 Bad Request", WebServer.JsonError("Invalid request."));
            return; //jer je receno samo GET 
         }

         var requestPath = tokens[1];
         if (requestPath == "/favicon.ico" || !requestPath.StartsWith("/search.json") || !requestPath.Contains("?"))
         {
            if (requestPath != "/favicon.ico")
               WebServer.WriteResponse(writer, "400 Bad Request", WebServer.JsonError("Nedostaje ili nevalidan 'query' parameter."));
            return;
         }

         //prvo proverim u kes, koristim URL putanju 
         if (cache.TryGetValue(requestPath, out var cachedEntry))
         {
            if (DateTime.Now < cachedEntry.ExpiryTime) //dal je validan tj isteko
            {
               WebServer.WriteResponse(writer, "200 OK", cachedEntry.Content);
               log($"Cache HIT (Validan): {requestPath}");
               return;
            }
            else
            {
               cache.TryRemove(requestPath, out _);
               log($"Cache ISTEKAO: {requestPath}. Ponovo pozivanje API-ja.");
            }
         }
         //ako nije u kesu idemo dalje
         var rawData = FetchOpenLibraryData(requestPath, writer, log);

         if (rawData.StartsWith("{\"error\":"))
         {
            return;
         }
         //filtriram ovaj Json
         string finalJson;
         try
         {
            finalJson = FilterAndSerialize(rawData, writer, log);
         }
         catch (Exception ex)
         {
            log($"Greška u obradi JSON-a: {ex.Message}");
            WebServer.WriteResponse(writer, "500 Internal Server Error", WebServer.JsonError("Greška u obradi API odgovora."));
            return;
         }

         //kesiram nove vrednosti
         var newExpiryTime = DateTime.Now.Add(expiryDuration);
         var newEntry = new CacheEntry(finalJson, newExpiryTime);
         //azuriram novo vreme
         cache.AddOrUpdate(requestPath, newEntry, (key, oldValue) => newEntry);
         //i saljem odg
         WebServer.WriteResponse(writer, "200 OK", finalJson);
      }

      private static string FilterAndSerialize(string rawData, StreamWriter writer, LogDelegate log)
      {
         using (var doc = JsonDocument.Parse(rawData))
         {
            var root = doc.RootElement;
            //ako je vratio numfound = 0 znaci da nema knjiga 
            if (root.TryGetProperty("numFound", out JsonElement numFoundElement) && numFoundElement.GetInt32() == 0)
            {
               WebServer.WriteResponse(writer, "404 Not Found", WebServer.JsonError("Nisu pronađene knjige."));
               throw new InvalidOperationException("Nema rezultata.");
            }

            if (root.TryGetProperty("error", out JsonElement errorElement)) //provera greske u API 
            {
               var message = errorElement.GetString();
               WebServer.WriteResponse(writer, "400 Bad Request", WebServer.JsonError($"Open Library API greška: {message}"));
               throw new InvalidOperationException($"Open Library API greška: {message}");
            }
            //filtriranje ako je sve ok
            if (root.TryGetProperty("docs", out JsonElement docsElement))
            {
               var filteredBooks = new List<object>();
               foreach (var book in docsElement.EnumerateArray())
               {
                  string? title = book.TryGetProperty("title", out var titleProp)
                        ? titleProp.GetString()
                        : "Nepoznato";

                  string author = "Nepoznato";
                  if (book.TryGetProperty("author_name", out var authorsProp) && authorsProp.ValueKind == JsonValueKind.Array)
                     author = string.Join(", ", authorsProp.EnumerateArray().Select(a => a.GetString()));

                  int year = book.TryGetProperty("first_publish_year", out var y) ? y.GetInt32() : 0;

                  //filteredBooks.Add(book);
                  filteredBooks.Add(new Book { Title = title, Author = author, Year = year });
               }

               return JsonSerializer.Serialize(filteredBooks, new JsonSerializerOptions { WriteIndented = true }); //lepsi json
            }
            throw new InvalidOperationException("Odgovor Open Library API-ja nema 'docs' element.");
         }
      }

      private static string FetchOpenLibraryData(string requestPath, StreamWriter writer, LogDelegate log)
      {
         try
         {
            var queryPart = WebUtility.UrlDecode(requestPath.TrimStart('/')); //url decode za %26 (&)
            var openLibraryUrl = $"https://openlibrary.org/{queryPart}"; //search.json?title=Harry%Potter

            //Ovde bi trebao async
            using (var client = new WebClient())
            {
               var response = client.DownloadString(openLibraryUrl);
               return response;
            }
         }
         catch (Exception ex)
         {
            log($"Error fetching from Open Library: {ex.Message}");
            WebServer.WriteResponse(writer, "502 Bad Gateway", WebServer.JsonError($"Greška pri komunikaciji sa Open Library: {ex.Message}"));
            return WebServer.JsonError("Indikacija greške");
         }
      }
   }
}