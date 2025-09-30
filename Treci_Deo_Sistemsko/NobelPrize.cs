using System;
using System.Collections.Generic;

namespace SysProj
{
   public class NobelPrize //model nobelove
   {
      public string Category { get; set; }
      public int AwardYear { get; set; }
      public List<Laureate> Laureates { get; set; } 
      public string DateAwarded { get; set; } //za analizu meseca

      public override string ToString()
      {
         var laureateNames = Laureates != null
             ? string.Join(", ", Laureates.Select(l => l.FullName))
             : "Nema laureata";

         return $"Godina: {AwardYear}, Kategorija: {Category}, Laureati: {laureateNames}, Motivacija: {Laureates?.FirstOrDefault()?.Motivation ?? "Nije dostupno"}";
      }
   }

   public class Laureate // model Dobitnika
   {
      public string FullName { get; set; }
      public string Motivation { get; set; }
   }
}