using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PrviDeo_OpenLibrary
{
   internal record struct CacheEntry(string Content, DateTime ExpiryTime); //ovo cuva json i vreme isteka 

   public static class WebServer
   {
      private static TcpListener listener; //Objekat koji slusa dolazne TCP veze na portu koji navedem
      private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new ConcurrentDictionary<string, CacheEntry>(); //kes, CD je thread safe, key je URL (string)
      private static readonly object logLock = new object(); //da bi samo 1 nit mogla da pise u konzoli
      private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(10); //max 10 zovu api istovremeno
      private const int Port = 8080;

      //timer za kes i exp vreme i interval kad se brise
      private static Timer cacheCleanupTimer;
      private static readonly TimeSpan CacheExpiryDuration = TimeSpan.FromMinutes(1);
      private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(30);

      public static void Start()
      {
         //inicijalizacija i pokretanje servera na bilo kojoj mreznoj adresi na def portu
         listener = new TcpListener(IPAddress.Any, Port); 
         listener.Start();
         WriteLog($"Server pokrenut na portu {Port}."); 

         // Inicijalizacija tajmera za ciscenje i brise cim krene i na svakih 30 sek
         cacheCleanupTimer = new Timer(
             state => CleanUpCache(),
             null,
             TimeSpan.Zero,
             CleanupInterval);

         while (true) 
         {
            var client = listener.AcceptTcpClient(); //trenutna nit stoji i ceka dok se novi klijent ne poveze
            ThreadPool.QueueUserWorkItem(HandleClient, client); // i kad se poveze obrada se predaje niti iz threadPool-a, a glavna se vraca na accept
         }
      }

      private static void CleanUpCache()
      {
         WriteLog($"[KEŠ ČIŠĆENJE] Pokrenuto čišćenje. Trenutno unosa: {Cache.Count}");

         int cleanedCount = 0;
         DateTime now = DateTime.Now;

         foreach (var pair in Cache)
         {
            if (now >= pair.Value.ExpiryTime) //ako je proslo vreme
            {
               if (Cache.TryRemove(pair.Key, out _)) // pokusavam da uklonim kljuc
               {
                  cleanedCount++;
               }
            }
         }
         WriteLog($"[KEŠ ČIŠĆENJE] Završeno. Obrisano unosa: {cleanedCount}. Preostalo unosa: {Cache.Count}"); //log
      }

      private static void HandleClient(object obj)
      {
         semaphore.Wait(); //samo 10 mogu da udju ovde

         try
         {
            if (obj is not TcpClient client) return;
            //using za automatski dispose mreznih resursa, cim se zavrsi posao
            using var clientResource = client;
            using var stream = clientResource.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            //Delegira svu poslovnu logiku klasi WebWorker
            WebWorker.ProcessRequest(client, reader, writer, Cache, WriteLog, CacheExpiryDuration);
         }
         catch (Exception ex)
         {
            WriteLog($"Greska kod klijenta: {ex.Message}");
            if (obj is TcpClient client && client.Connected)
            {
               try
               {
                  var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
                  WriteResponse(writer, "500 Internal Server Error", JsonError("Internal server error."));
               }
               catch {}
            }
         }
         finally
         {
            semaphore.Release(); //oslobadja , neko drugi moze da udje
         }
      }

      internal static void WriteResponse(StreamWriter writer, string status, string content)
      {
         if (!writer.BaseStream.CanWrite) return;

         writer.WriteLine($"HTTP/1.1 {status}");
         writer.WriteLine("Content-Type: application/json; charset=UTF-8");
         writer.WriteLine($"Content-Length: {Encoding.UTF8.GetByteCount(content)}");
         writer.WriteLine();
         writer.WriteLine(content);
      }

      internal static string JsonError(string message)
      {
         return JsonSerializer.Serialize(new { error = message });
      }

      internal static void WriteLog(string message)
      {
         lock (logLock)
         {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
         }
      }
   }
}