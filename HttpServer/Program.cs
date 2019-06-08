using System;

namespace HttpServer
{
    class Program
    {
        
        static void Main(string[] args)
        {           
            Console.WriteLine("Starting server on port 8080");
            HttpServer server = new HttpServer(8080);
            server.Start();          

        }
    }
}
