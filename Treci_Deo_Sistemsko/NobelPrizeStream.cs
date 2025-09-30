using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace SysProj
{
   // Stream nagrada - odgovara ulozi IObservable<NobelPrize>
   public class NobelPrizeStream : IObservable<NobelPrize>
   {
      private readonly Subject<NobelPrize> prizeSubject = new Subject<NobelPrize>();
      private readonly NobelPrizeService prizeService = new NobelPrizeService();
      public string Category { get; }
      private readonly string category;

      public NobelPrizeStream(string category)
      {
         this.category = category;
         this.Category = category; // Postavljam javni properti
      }

      public async Task GetPrizesAsync()
      {
         try
         {
            var prizes = await prizeService.FetchPrizesAsync(category);
            foreach (var prize in prizes)
            {
               prizeSubject.OnNext(prize); 
            }
            prizeSubject.OnCompleted(); 
         }
         catch (Exception ex)
         {
            prizeSubject.OnError(ex);
         }
      }

      public IDisposable Subscribe(IObserver<NobelPrize> observer)
      {
         return prizeSubject.Subscribe(observer);
      }
   }
}