using Bungie;
using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Diagnostics;
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
            string bsp_path = Console.ReadLine().Trim('"');
            if (bsp_path.EndsWith(".xml") || bsp_path.EndsWith(".txt")) // Should really be .xml, but we'll let .xml slide too (until it crashes :) )
            {
                bsp_paths.Add(bsp_path);
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
        List<string> all_h2_shader_paths = new List<string>();

        ManagedBlamSystem.InitializeProject(InitializationType.TagsOnly, h3ek_path);
        foreach (string bsp in bsp_paths)
        {
            List<string> bsp_shader_paths = Convert_XML(bsp, h2ek_path, h3ek_path);
            foreach (string path in bsp_shader_paths)
            {
                string full_path = h2ek_path + @"\tags\" + path + ".shader";
                if (!all_h2_shader_paths.Contains(full_path))
                {
                    all_h2_shader_paths.Add(full_path);
                } 
            }
        }

        // Create shader xml export folder
        string folderName = "shader_xml";
        string folderPath = Path.Combine(Directory.GetCurrentDirectory(), folderName);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        else
        {
            // Delete existing XML files
            string[] xmlFiles = Directory.GetFiles(folderPath, "*.xml");
            foreach (string xmlFile in xmlFiles)
            {
                File.Delete(xmlFile);
            }
        }
        Console.WriteLine("\nBeginning .shader to .xml conversion...\nPlease wait...");
        ShaderExtractor(all_h2_shader_paths, h2ek_path, folderPath);
        Console.WriteLine("\nAll shaders converted to XML!");
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

    static void ShaderExtractor(List<string> shader_paths, string h2ek_path, string xml_output_path)
    {
        string tool_path = h2ek_path + @"\tool.exe";

        foreach (string shader_path in shader_paths)
        {
            List<string> argumentList = new List<string>
            {
                "export-tag-to-xml"
            };
            argumentList.Add("\"" + shader_path + "\"");
            argumentList.Add("\"" + xml_output_path + "\\" + shader_path.Split('\\').Last().Replace(".shader", "") + ".xml" + "\"");

            string arguments = string.Join(" ", argumentList);

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = tool_path,
                Arguments = arguments,
                WorkingDirectory = h2ek_path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process process = new Process
            {
                StartInfo = processStartInfo
            };

            process.Start();
            process.WaitForExit();
            process.Close();
        }
    }
}