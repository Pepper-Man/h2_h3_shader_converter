using Bungie;
using System;
using System.Xml;
using System.Linq;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        List<string> bsp_paths = new List<string>();


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
        bsp_paths.Add(@"G:\Steam\steamapps\common\H2EK\ascension_bsp0.xml");

        string h2ek_path = bsp_paths[0].Substring(0, bsp_paths[0].IndexOf("H2EK") + "H2EK".Length);
        string h3ek_path = @"C:\Program Files (x86)\Steam\steamapps\common\H3EK";
        List<string> all_h2_shader_paths= new List<string>();

        ManagedBlamSystem.InitializeProject(InitializationType.TagsOnly, h3ek_path);
        foreach (string bsp in bsp_paths)
        {
            List<string> bsp_shader_paths = Convert_XML(bsp, h2ek_path, h3ek_path);
            foreach (string path in bsp_shader_paths)
            {
                all_h2_shader_paths.Add(path);
            }
        }
        
    }

    static List<string> Convert_XML(string bsp_path, string h2ek_path, string h3ek_path)
    {
        Console.WriteLine("Beginning parsing XML");

        XmlDocument bspfile = new XmlDocument();
        bspfile.Load(bsp_path);
        XmlNode root = bspfile.DocumentElement;

        XmlNodeList materials_block = root.SelectNodes(".//block[@name='materials']");
        List<string> shader_paths = new List<string>();

        foreach (XmlNode material in materials_block)
        {
            bool end = false;
            int i = 0;
            while (!end)
            {
                XmlNode element = material.SelectSingleNode("./element[@index='" + i + "']");
                if (element != null)
                {
                    shader_paths.Add(element.SelectSingleNode("./tag_reference[@name='shader']").InnerText.Trim());
                    i++;
                }
                else
                {
                    end = true;
                    Console.WriteLine("\nFinished getting materials in bsp \"" + bsp_path + "\"\n");
                    Console.WriteLine("Shaders list:\n");
                    foreach (string shader in shader_paths)
                    {
                        Console.WriteLine(shader);
                    }
                }
            }
        }
        return shader_paths;
    }
}