using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SysProj
{
   public class NobelPrizeService
   {
      private readonly HttpClient client = new HttpClient();
      private const string BaseUrl = "https://api.nobelprize.org/2.1/nobelPrizes";


      // pribavljanje nagrade za kategoriju: "che", "phy", "lit" itd..
      public async Task<IEnumerable<NobelPrize>> FetchPrizesAsync(string category)
      {
         // API prima parametar nobelPrizeCategory
         var url = $"{BaseUrl}?nobelPrizeCategory={category}&format=json&limit=50";

         try
         {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var jsonResponse = JObject.Parse(content);
            var prizesJson = jsonResponse["nobelPrizes"] as JArray;

            if (prizesJson == null)
            {
               return Enumerable.Empty<NobelPrize>();
            }

            // Mapiranje JSON-a u listu NobelPrize objekata
            return prizesJson.Select(prizeJson =>
            {
               var laureates = (prizeJson["laureates"] as JArray)?
                   .Select(l => new Laureate
                   {
                      // API vraća 'fullName' za pojedince
                      FullName = (string)l["fullName"]?["en"] ?? (string)l["orgName"]?["en"] ?? "Nije dostupno",
                      // API vraća 'motivation'
                      Motivation = (string)l["motivation"]?["en"] ?? "Nije dostupno"
                   }).ToList() ?? new List<Laureate>();

               return new NobelPrize
               {
                  Category = (string)prizeJson["category"]["en"],
                  AwardYear = (int)prizeJson["awardYear"],
                  DateAwarded = (string)prizeJson["dateAwarded"],
                  Laureates = laureates
               };
            });
         }
         catch (Exception ex)
         {
            Console.WriteLine($"Greska pri pribavljanju nagrada: {ex.Message}");
            return Enumerable.Empty<NobelPrize>();
         }
      }
   }
}