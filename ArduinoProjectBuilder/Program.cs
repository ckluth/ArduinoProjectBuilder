using System.ComponentModel.Design;
using System.Reflection;
using System.Text;

namespace ArduinoProjectBuilder
{
    internal class Program
    {
        //static void Test()
        //{
        //    var str = ResourceReader.ExtractString("NewProject.ini");
        //    Console.WriteLine(str);
        //}

        static void Main(string[] args)
        {
            //Test();
            //Console.ReadKey();
            //return;

#if DEBUG
            args = [@"C:\Users\chris\OneDrive\Making\Arduino\Builder\NewProject.ini"];
            //args = [@"C:\Users\chris\OneDrive\Making\Arduino\Builder\EmptyProject.ini"];
#endif
            //var cfg = Builder.GetConfig(true);

            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("invalid arguments: [projectIniFilepath]");
                    return;
                }

                var projectIniFilepath = args[0];
                if (!File.Exists(projectIniFilepath))
                {
                    Console.WriteLine($"ini file not found: {projectIniFilepath}");
                    return;
                }

                try
                {
                    var newproject = Builder.Run(projectIniFilepath);
                    Console.WriteLine(string.IsNullOrEmpty(newproject)
                        ? $"Something went wrong."
                        : $"Project {newproject} successfully created.");
                }
                catch(Exception ex)
                {
                    Console.WriteLine("something went wrong:");
                    Console.WriteLine();
                    Console.WriteLine(ex);
                }
            }
            finally
            {
                Console.WriteLine("\r\n[Press any key...]");
                Console.ReadKey();
            }
        }
    }
}
