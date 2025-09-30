using System;

namespace PrviDeo_OpenLibrary
{
   class Program
   {
      static void Main(string[] args)
      {
         WebServer.Start();
         //http://localhost:8080/search.json?title=Harry%Potter
         //http://localhost:8080/search.json?author=tolkien&sort=new
         //http://localhost:8080/search.json?title=Harry%Potter&author=tolkien

      }
   }
}