using System;
using System.IO;
using System.Reflection;
using NLog;

namespace ProxyChanger
{
    class Program
    {
        private static readonly string CurrentMethodDeclaringType = MethodBase.GetCurrentMethod().DeclaringType.ToString();
        private static readonly Logger Log = LogManager.GetLogger(CurrentMethodDeclaringType);

        

        static void Main(string[] args)
        {
            Log.Info("Launch an application...");
            if (!SetCurrentDirectory()) return;
            var worker = new Worker(Log);
            
            worker.Run();
            Console.WriteLine("Press any key to exits");
            Console.ReadLine();
            worker.Stop();


            Log.Info("Application completed.");
        }

        private static bool SetCurrentDirectory()
        {
            string location = typeof(Program).Assembly.Location;
            string currentDirectory = Path.GetDirectoryName(location);
            if (string.IsNullOrEmpty(currentDirectory))
            {
                Log.Error("Failed to establish a directory to work");
                return false;
            }
            Environment.CurrentDirectory = currentDirectory;
            return true;
        }

        
    }
}
