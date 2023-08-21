using Bungie;
using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

class Shader
{
    public List<Parameter> parameters { get; set; }
}

class Parameter
{
    public string name { get; set; }
    public string type { get; set; }
    public string bitmap { get; set; }
    public string value { get; set; }
    public string colour { get; set; }
}

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
        string shaders_output_path = @"C:\Program Files (x86)\Steam\steamapps\common\H3EK\tags\halo_2\levels\ascension\shaders";

        string h2ek_path = bsp_paths[0].Substring(0, bsp_paths[0].IndexOf("H2EK") + "H2EK".Length);
        string h3ek_path = @"C:\Program Files (x86)\Steam\steamapps\common\H3EK";
        List<string> all_h2_shader_paths = new List<string>();

        ManagedBlamSystem.InitializeProject(InitializationType.TagsOnly, h3ek_path);
        foreach (string bsp in bsp_paths)
        {
            List<string> bsp_shader_paths = GetShaders(bsp);
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
        string xml_output_path = Path.Combine(Directory.GetCurrentDirectory(), "shader_xml");
        if (!Directory.Exists(xml_output_path))
        {
            Directory.CreateDirectory(xml_output_path);
        }
        else
        {
            // Delete existing XML files
            string[] xmlFiles = Directory.GetFiles(xml_output_path, "*.xml");
            foreach (string xmlFile in xmlFiles)
            {
                File.Delete(xmlFile);
            }
        }

        // Create bitmap tga export folder
        string tga_output_path = Path.Combine(Directory.GetCurrentDirectory(), "textures_output");
        if (!Directory.Exists(tga_output_path))
        {
            Directory.CreateDirectory(tga_output_path);
        }
        else
        {
            // Delete existing XML files
            string[] tgaFiles = Directory.GetFiles(tga_output_path, "*.tga");
            foreach (string tgaFile in tgaFiles)
            {
                File.Delete(tgaFile);
            }
        }

        Console.WriteLine("\nBeginning .shader to .xml conversion...\nPlease wait...");
        ShaderExtractor(all_h2_shader_paths, h2ek_path, xml_output_path);
        Console.WriteLine("\nAll shaders converted to XML!\n\nGrabbing all referenced bitmap paths:\n");
        List<Shader> all_shader_data = GetShaderData(xml_output_path);
        List<string> all_bitmap_refs = new List<string>();
        
        foreach (Shader shader in all_shader_data)
        {
            foreach (Parameter param in shader.parameters)
            {
                if (!String.IsNullOrEmpty(param.bitmap))
                {
                    if (!all_bitmap_refs.Contains(param.bitmap))
                    {
                        all_bitmap_refs.Add(param.bitmap);
                        Console.WriteLine(param.bitmap);
                    }
                }
            }
        }

        Console.WriteLine("\nObtained all referenced bitmaps!\n\nExtracting bitmap tags to TGA...");
        ExtractBitmaps(all_bitmap_refs, h2ek_path, tga_output_path);
        Console.WriteLine("\nExtracted all bitmap to .TGA\nRunning .TIF conversion process...");
    }

    static List<string> GetShaders(string bsp_path)
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
                "export-tag-to-xml",
                "\"" + shader_path + "\"",
                "\"" + xml_output_path + "\\" + shader_path.Split('\\').Last().Replace(".shader", "") + ".xml" + "\""
            };

            string arguments = string.Join(" ", argumentList);

            RunTool(tool_path, arguments, h2ek_path);
        }
    }

    static void RunTool(string tool_path, string arguments, string h2ek_path)
    {
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
    
    static List<Shader> GetShaderData(string xml_output_path)
    {
        List<Shader> all_shader_data = new List<Shader>();
        string[] xml_files = Directory.GetFiles(xml_output_path, "*.xml");
        foreach (string xml_file in xml_files)
        {
            XmlDocument shader_file = new XmlDocument();
            shader_file.Load(xml_file);
            XmlNode root = shader_file.DocumentElement;
            List<Parameter> shader_parameters = new List<Parameter>();

            XmlNodeList params_block = root.SelectNodes(".//block[@name='parameters']");

            foreach (XmlNode param in params_block)
            {
                bool end = false;
                int i = 0;
                while (!end)
                {
                    XmlNode element = param.SelectSingleNode("./element[@index='" + i + "']");
                    if (element != null)
                    {
                        string prm_name = element.SelectSingleNode("./field[@name='name']").InnerText.Trim();
                        string prm_type = element.SelectSingleNode("./field[@name='type']").InnerText.Trim();
                        string prm_bitmap = element.SelectSingleNode("./tag_reference[@name='bitmap']").InnerText.Trim();
                        string prm_value = element.SelectSingleNode("./field[@name='const value']").InnerText.Trim();
                        string prm_colour = element.SelectSingleNode("./field[@name='const color']").InnerText.Trim();

                        shader_parameters.Add(new Parameter
                        {
                            name = prm_name,
                            type = prm_type,
                            bitmap = prm_bitmap,
                            value = prm_value,
                            colour = prm_colour
                        });

                        i++;
                    }
                    else
                    {
                        end = true;
                    }
                }
            }
            all_shader_data.Add(new Shader
            {
                parameters= shader_parameters
            });
        }
        return all_shader_data;
    }

    static void ExtractBitmaps(List<string> all_bitmap_refs, string h2ek_path, string tga_output_path)
    {
        string tool_path = h2ek_path + @"\tool.exe";

        foreach (string bitmap in all_bitmap_refs)
        {
            List<string> argumentList = new List<string>
            {
                "export-bitmap-tga",
                "\"" + bitmap + "\"",
                "\"" + tga_output_path + "\\\\" + "\""
            };

            string arguments = string.Join(" ", argumentList);
            

            RunTool(tool_path, arguments, h2ek_path);
            Console.WriteLine("Extracted " + bitmap);
        }
    }
    
    static void TGAToTIF()
    {

    }
}