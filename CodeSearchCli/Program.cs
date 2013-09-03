using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeSearch;

namespace CodeSearchCli
{
    class Program
    {
        static void Main(string[] args)
        {
            var core = new Core("http://10.0.0.3:9200","filerepo","fileinformation");

            //core.Start(@"C:\Users\Austin\code_git\axis",m=>Console.WriteLine(m));
            core.Start(@"C:\csharp", m => Console.WriteLine(m));

            Console.Read();
            
        }
    }
}
