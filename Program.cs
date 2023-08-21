using System;

class Program
{
    static void Main(string[] args)
    {
        string scen_path;

        Console.WriteLine("H2 to H3 Shader Converter by PepperMan\n\n");
        /*
        while (true)
        {
            Console.WriteLine("\nPlease enter the path to an exported H2 scenario XML file.\nThis must be the full path with file extension - This is the scenario the shaders list will be grabbed from:");
            scen_path = Console.ReadLine().Trim('"');
            if (scen_path.EndsWith(".xml") || scen_path.EndsWith(".txt")) // Should really be .xml, but we'll let .xml slide too (until it crashes :) )
            {
                break;
            }
            else
            {
                Console.WriteLine("\nFile doesn't look like a .txt or .xml file. Please try again.");
            }
        }
        */
        // Temporary hardcoding for quick debugging
        scen_path = @"G:\Steam\steamapps\common\H2EK\ascension_output.xml";

        string h2ek_path = scen_path.Substring(0, scen_path.IndexOf("H2EK") + "H2EK".Length);
        string h3ek_path = @"C:\Program Files (x86)\Steam\steamapps\common\H3EK";
    }
}