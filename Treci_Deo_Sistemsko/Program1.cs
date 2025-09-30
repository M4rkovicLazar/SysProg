//using SysProj;
//using System;
//using System.Collections.Generic;
//using System.Reactive.Linq;
//using System.Threading.Tasks;

//public class Program
//{
//   public static async Task Main()
//   {
//      // 1. KREIRANJE VIŠE STREAMA (za svaku kategoriju)

//      // Stream za nagrade iz FIZIKE ('phy')
//      var physicsStream = new NobelPrizeStream("phy");

//      // Stream za nagrade iz MEDICINE ('med')
//      var medicineStream = new NobelPrizeStream("med");


//      // 2. SPAJANJE STREAMA U JEDAN (MERGE)

//      // Koristimo operator Observable.Merge() da kombinujemo oba toka u jedan.
//      // Podaci iz oba streama će stizati u 'combinedStream' čim budu dostupni.
//      var combinedStream = Observable.Merge(physicsStream, medicineStream);


//      // 3. KREIRANJE OBSERVERA I PRETPLATA

//      var generalObserver = new NobelPrizeObserver("General Observer - FIZIKA I MEDICINA");

//      // Pretplaćujemo se na spojeni stream
//      var subscriptionGeneral = combinedStream.Subscribe(generalObserver);


//      // 4. IMPLEMENTACIJA LOGIKE ZA ANALIZU MESECA
//      // Logika ostaje ista, ali sada radi nad spojenim streamom.
//      var monthAnalysis = combinedStream.Select(prize =>
//      {
//         // ... (logika za izdvajanje meseca ostaje ista)
//         if (DateTime.TryParse(prize.DateAwarded, out DateTime date))
//         {
//            return date.Month;
//         }
//         return -1;
//      })
//      .Where(month => month != -1)
//      .ToList()
//      .Subscribe(months =>
//      {
//         // Logika za pronalazak najčešćeg meseca se izvršava na svim podacima (fizika + medicina)
//         if (months == null || months.Count == 0)
//         {
//            Console.WriteLine("\nNema dostupnih datuma za analizu meseca.");
//            return;
//         }

//         var monthCounts = months
//               .GroupBy(month => month)
//               .Select(group => new { Month = group.Key, Count = group.Count() })
//               .OrderByDescending(x => x.Count)
//               .FirstOrDefault();

//         if (monthCounts != null)
//         {
//            var monthName = new DateTime(2000, monthCounts.Month, 1).ToString("MMMM");
//            Console.WriteLine($"\n--- ANALIZA (Fizika + Medicina) ---");
//            Console.WriteLine($"Mesec sa najvećim brojem dodela: {monthName} (Broj dodela: {monthCounts.Count})");
//            Console.WriteLine($"-------------------------------------\n");
//         }
//      });


//      // 5. POKRETANJE ASINHRONOG PRIBAVLJANJA PODATAKA ZA SVE STREAME

//      // Moramo pokrenuti GetPrizesAsync() za SVAKI pojedinačni stream.
//      // Pošto se obe metode pokreću asinhrono, podaci će stizati u 'combinedStream' čim budu spremni.
//      var task1 = physicsStream.GetPrizesAsync();
//      var task2 = medicineStream.GetPrizesAsync();

//      await Task.WhenAll(task1, task2); // Čekamo da se oba poziva završe

//      Console.WriteLine("Pritisnite ENTER za izlaz...");
//      Console.ReadLine();

//      // 6. Oslobađanje resursa
//      subscriptionGeneral.Dispose();
//      monthAnalysis.Dispose();
//   }
//}