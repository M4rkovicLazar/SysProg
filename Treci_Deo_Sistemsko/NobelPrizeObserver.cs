using System;

namespace SysProj
{
   // Klasa koja reaguje na nove objekte (nagrade)
   public class NobelPrizeObserver : IObserver<NobelPrize>
   {
      private readonly string name;
      public NobelPrizeObserver(string name)
      {
         this.name = name;
      }

      public void OnNext(NobelPrize prize)
      {
         Console.WriteLine($"\n--- {name} ---");
         Console.WriteLine($"Kategorija: {prize.Category} ({prize.AwardYear})");
         foreach (var laureate in prize.Laureates)
         {
            Console.WriteLine($"  Laureat: {laureate.FullName}");
            Console.WriteLine($"  Motivacija: {laureate.Motivation}");
         }
      }

      public void OnError(Exception e)
      {
         Console.WriteLine($"\nError!\n {name}: Doslo je do greske!: {e.Message}");
      }

      public void OnCompleted()
      {
         Console.WriteLine($"\n--- {name}: Svi podaci o Nobelovim nagradama su uspesno pribavljeni. ---");
      }
   }
}