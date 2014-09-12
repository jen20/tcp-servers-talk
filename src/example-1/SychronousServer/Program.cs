using System;

namespace SynchronousServer 
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("Press <Ctrl+C> to stop the server");
            new PrettyBadServer().Run();
            Console.ReadLine();
        }
    }
} 