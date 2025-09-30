using SysProj;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

public class Program
{
   public static async Task Main()
   {
      Console.WriteLine("Unesite kategorije Nobelovih nagrada (npr. 'phy, med, lit, che'):");
      string input = Console.ReadLine();

      var categories = input?.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(c => c.Trim().ToLower())
                            .ToList() ?? new List<string>();

      if (categories.Count == 0)
      {
         Console.WriteLine("Niste uneli nijednu validnu kategoriju. Izlazak iz programa.");
         return;
      }

      Console.WriteLine($"Pribavljaju se podaci za kategorije: {string.Join(", ", categories)}\n");

      var streams = categories.Select(category => new NobelPrizeStream(category) as IObservable<NobelPrize>).ToList();

      //spajam sve kreirane tokove u 1 sa Megre()
      var combinedStream = Observable.Merge(streams);
      var observerName = $"Observer - {string.Join(", ", categories).ToUpper()}";
      var generalObserver = new NobelPrizeObserver(observerName);

      //sub se na combinedStream, a podaci iz svih kat ce da dolaze ovde
      var subscriptionGeneral = combinedStream.Subscribe(generalObserver);

      var monthAnalysis = combinedStream.Select(prize =>
      {
         if (DateTime.TryParse(prize.DateAwarded, out DateTime date))
         {
            return date.Month;
         }
         return -1;
      })
      .Where(month => month != -1)
      .ToList()
      .Subscribe(months =>
      {
         if (months != null && months.Count > 0)
         {
            var monthCounts = months
                  .GroupBy(month => month)
                  .Select(group => new { Month = group.Key, Count = group.Count() })
                  .OrderByDescending(x => x.Count)
                  .FirstOrDefault();

            if (monthCounts != null)
            {
               var monthName = new DateTime(2000, monthCounts.Month, 1).ToString("MMMM");
               Console.WriteLine($"\n--- ZBIRNA ANALIZA ({string.Join(", ", categories).ToUpper()}) ---");
               Console.WriteLine($"Mesec sa najvećim brojem dodela: {monthName} (Broj dodela: {monthCounts.Count})");
               Console.WriteLine($"--------------------------------------------------\n");
            }
         }
         else
            Console.WriteLine("\nNema dostupnih datuma za analizu meseca u unesenim kategorijama.");
      });
      //za svaku kreiranu stream instancu pokrecem GetPrizesAsync()
      var fetchTasks = categories.Select(category =>
          ((NobelPrizeStream)streams.First(s => ((NobelPrizeStream)s).Category == category)).GetPrizesAsync()).ToList();

      await Task.WhenAll(fetchTasks); // cekam da se svi resursi zavrse

      Console.WriteLine("Pritisnite ENTER za izlaz...");
      Console.ReadLine();

      // Oslobadjam resurse
      subscriptionGeneral.Dispose();
      monthAnalysis.Dispose();
   }
}