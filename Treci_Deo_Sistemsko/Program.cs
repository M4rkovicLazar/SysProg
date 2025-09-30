//using SysProj;
//using System.Reactive.Linq;

//public class Program
//{
//   public static async Task Main()
//   {
//      // 1. Kreiramo stream za Nobelove nagrade za željenu kategoriju (npr. 'phy' za fiziku)
//      // API dokumentacija: medicine, physics, chemistry, literature, peace, economic sciences
//      var categ = Console.ReadLine();
//      var prizeStream = new NobelPrizeStream("phy");

//      // 2. Kreiramo observer za prikaz rezultata
//      var generalObserver = new NobelPrizeObserver("General Observer - Nobel Prize");

//      // 3. Povezujemo general observer direktno na stream
//      var subscriptionGeneral = prizeStream.Subscribe(generalObserver);

//      // 4. Implementacija logike za pronalazak meseca sa najviše dodela (Koristeći Rx.NET operatore)

//      // A. Koristimo 'Select' da izvučemo mesec iz datuma dodele (DateAwarded property)
//      var monthStream = prizeStream.Select(prize =>
//      {
//         if (DateTime.TryParse(prize.DateAwarded, out DateTime date))
//         {
//            return date.Month; // Vraća int meseca (1-12)
//         }
//         return -1; // -1 za nagrade bez validnog datuma
//      })
//      .Where(month => month != -1); // Filtriramo nevalidne datume

//      // B. Koristimo 'ToList()' operator da prikupimo SVE mesece pre analize (akumulacija)
//      var monthAnalysis = monthStream
//          .ToList() // Akumulira sve elemente u streamu u jednu List<int>
//          .Subscribe(months =>
//          {
//             // Ova logika se izvršava SAMO JEDNOM, kada je ceo stream završen (OnCompleted)

//             if (months == null || months.Count == 0)
//             {
//                Console.WriteLine("\nNema dostupnih datuma za analizu meseca.");
//                return;
//             }

//             // C. Logika za nalaženje najčešćeg meseca (Van Rx streama, unutar Subscribe bloka)
//             var monthCounts = months
//                   .GroupBy(month => month)
//                   .Select(group => new { Month = group.Key, Count = group.Count() })
//                   .OrderByDescending(x => x.Count)
//                   .FirstOrDefault();

//             if (monthCounts != null)
//             {
//                var monthName = new DateTime(2000, monthCounts.Month, 1).ToString("MMMM");
//                Console.WriteLine($"\n--- ANALIZA ---");
//                Console.WriteLine($"Mesec sa najvećim brojem dodela: {monthName} (Broj dodela: {monthCounts.Count})");
//                Console.WriteLine($"---------------\n");
//             }
//          });

//      // 5. Pribavljamo podatke - ovo pokreće ceo lanac
//      await prizeStream.GetPrizesAsync();

//      Console.WriteLine("Pritisnite ENTER za izlaz...");
//      Console.ReadLine();

//      // 6. Oslobađanje resursa
//      subscriptionGeneral.Dispose();
//      monthAnalysis.Dispose();
//   }
//}