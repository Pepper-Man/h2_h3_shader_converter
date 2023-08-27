﻿using Bungie;
using System;
using System.IO;
using System.Xml;
using Bungie.Tags;
using System.Linq;
using ImageMagick;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Xml.Linq;

class Shader
{
    public string name { get; set; }
    public string glob_mat { get; set; }
    public string template { get; set; }
    public List<Parameter> parameters { get; set; }
}

class Parameter
{
    public string name { get; set; }
    public string type { get; set; }
    public string bitmap { get; set; }
    public string value { get; set; }
    public string colour { get; set; }
    public sbyte scalex_1 { get; set; }
    public byte scalex_2 { get; set; }
    public sbyte scaley_1 { get; set; }
    public byte scaley_2 { get; set; }
    public byte[] scaley { get; set; }
}

class BitmapData
{
    public string bitmap { get; set; }
    public string type { get; set; }
    public string compr { get; set; }
    public string fade { get; set; }
    public string bmp_hgt { get; set; }
}

class Program
{
    static async Task Main(string[] args)
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
        bsp_paths.Add(@"G:\Steam\steamapps\common\H2EK\tags\scenarios\solo\03a_oldmombasa\earthcity_1.xml");
        string h3_scen = @"C:\Program Files (x86)\Steam\steamapps\common\H3EK\tags\halo_2\levels\singleplayer\oldmombasa\oldmombasa.scenario";

        string bitmaps_dir = (h3_scen.Substring(0, h3_scen.LastIndexOf('\\')) + "\\bitmaps").Replace("tags", "data");
        string h2ek_path = bsp_paths[0].Substring(0, bsp_paths[0].IndexOf("H2EK") + "H2EK".Length);
        string h3ek_path = @"C:\Program Files (x86)\Steam\steamapps\common\H3EK";
        List<string> all_h2_shader_paths = new List<string>();

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
            // Delete existing TGA/TIF files
            string[] tgaFiles = Directory.GetFiles(tga_output_path, "*.tga");
            string[] tifFiles = Directory.GetFiles(tga_output_path, "*.tif");
            foreach (string tgaFile in tgaFiles)
            {
                File.Delete(tgaFile);
            }
            foreach (string tifFile in tifFiles)
            {
                File.Delete(tifFile);
            }
        }

        // Create bitmap xml folder
        if (!Directory.Exists(tga_output_path.Replace("textures_output", "bitmap_xml")))
        {
            Directory.CreateDirectory(tga_output_path.Replace("textures_output", "bitmap_xml"));
        }
        else
        {
            // Delete existing XML files
            string[] xmlFiles = Directory.GetFiles(tga_output_path.Replace("textures_output", "bitmap_xml"), "*.xml");
            foreach (string xmlFile in xmlFiles)
            {
                File.Delete(xmlFile);
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
        Task task = ExtractBitmaps(all_bitmap_refs, h2ek_path, tga_output_path);
        await WaitForTaskCompletion(task);
        Console.WriteLine("\nExtracted all bitmap to .TGA\nRunning .TIF conversion process...");
        ManagedBlamSystem.InitializeProject(InitializationType.TagsOnly, h3ek_path);
        string[] errors = TGAToTIF(tga_output_path, bitmaps_dir, h3ek_path);
        Console.WriteLine("\nFinished importing bitmaps into H3.\nCreating H3 shader tags...");
        MakeShaderTags(all_shader_data, bitmaps_dir, h3ek_path);
        Console.WriteLine("\nSuccessfully created all shader tags.");
        Console.WriteLine("The following errors were caught:\n");
        foreach (string bitmap_issue in errors)
        {
            Console.WriteLine(bitmap_issue);
        }
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

    static void RunTool(string tool_path, string arguments, string ek_path)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = tool_path,
            Arguments = arguments,
            WorkingDirectory = ek_path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };

        Process process = new Process
        {
            StartInfo = processStartInfo
        };

        if (tool_path.Contains("H3EK"))
        {
            process.OutputDataReceived += Process_OutputDataReceived;
            process.ErrorDataReceived += Process_ErrorDataReceived;
        }
        
        process.Start();
        if (tool_path.Contains("H3EK"))
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        // Wait for the process to exit or the timeout to elapse
        Task processTask = Task.Run(() =>
        {
            if (!process.WaitForExit(5 * 1000)) // Wait with timeout
            {
                // The process did not exit within the timeout, so handle this case
                // For example, you could log an error or perform cleanup
            }
        });

        processTask.Wait(); // Wait for the process task to complete

        process.Close();
    }

    private static void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            Console.WriteLine(e.Data);
        }
    }

    private static void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            Console.WriteLine("Error: " + e.Data);
        }
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
            string shader_name = (new DirectoryInfo(xml_file).Name).Replace(".xml", "");

            XmlNodeList params_block = root.SelectNodes(".//block[@name='parameters']");
            string shd_templ = root.SelectSingleNode("./tag_reference[@name='template']").InnerText.Trim();
            string shd_globmat = root.SelectSingleNode("./field[@name='material name']").InnerText.Trim();

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
                        XmlNode anim_data_block = element.SelectSingleNode("./block[@name='animation properties']");
                        sbyte byte1_scaleX = new sbyte();
                        byte byte2_scaleX = new byte();
                        sbyte byte1_scaleY = new sbyte();
                        byte byte2_scaleY = new byte();

                        if (anim_data_block != null)
                        {
                            // Animation data exists
                            foreach (XmlNode anim_data in anim_data_block)
                            {
                                string type = anim_data.SelectSingleNode("./field[@name='type']").InnerText.Trim();
                                if (type.Contains("bitmap scale x"))
                                {
                                    // Grab x scale
                                    XmlNode data_block = anim_data.SelectSingleNode("./block[@name='data']");
                                    foreach (XmlNode index in data_block)
                                    {
                                        // Indices 6 and 7 contain the scale value bytes
                                        if (index.Attributes["index"]?.Value == "6")
                                        {
                                            byte1_scaleX = sbyte.Parse(index.SelectSingleNode("./field[@name='Value']").InnerText.Trim());
                                        }
                                        else if (index.Attributes["index"]?.Value == "7")
                                        {
                                            byte2_scaleX = byte.Parse(index.SelectSingleNode("./field[@name='Value']").InnerText.Trim());
                                            break;
                                        }
                                    }
                                }
                                else if (type.Contains("bitmap scale y"))
                                {
                                    // Grab y scale
                                    XmlNode data_block = anim_data.SelectSingleNode("./block[@name='data']");
                                    foreach (XmlNode index in data_block)
                                    {
                                        // Indices 6 and 7 contain the scale value bytes
                                        if (index.Attributes["index"]?.Value == "6")
                                        {
                                            byte1_scaleY = sbyte.Parse(index.SelectSingleNode("./field[@name='Value']").InnerText.Trim());
                                        }
                                        else if (index.Attributes["index"]?.Value == "7")
                                        {
                                            byte2_scaleY = byte.Parse(index.SelectSingleNode("./field[@name='Value']").InnerText.Trim());
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        

                        shader_parameters.Add(new Parameter
                        {
                            name = prm_name,
                            type = prm_type,
                            bitmap = prm_bitmap,
                            value = prm_value,
                            colour = prm_colour,
                            scalex_1 = byte1_scaleX,
                            scalex_2 = byte2_scaleX,
                            scaley_1 = byte1_scaleY,
                            scaley_2 = byte2_scaleY,
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
                name = shader_name,
                glob_mat = shd_globmat,
                template = shd_templ,
                parameters = shader_parameters
            }); ;
        }
        return all_shader_data;
    }

    static async Task ExtractBitmaps(List<string> all_bitmap_refs, string h2ek_path, string tga_output_path)
    {
        List<Task> tasks = new List<Task>();
        string tool_path = h2ek_path + @"\tool.exe";

        foreach (string bitmap in all_bitmap_refs)
        {
            // Extracting bitmap to TGA
            List<string> argumentListTGA = new List<string>
            {
                "export-bitmap-tga",
                "\"" + bitmap + "\"",
                "\"" + tga_output_path + "\\\\" + "\""
            };

            string arguments = string.Join(" ", argumentListTGA);
            tasks.Add(Task.Run(() => RunTool(tool_path, arguments, h2ek_path)));
            
            Console.WriteLine("Extracted " + bitmap);
        }

        await Task.WhenAll(tasks);

        tasks.Clear();

        foreach (string bitmap in all_bitmap_refs)
        {
            // Extracting bitmap to XML
            List<string> argumentListXML = new List<string>
            {
                "export-tag-to-xml",
                "\"" + h2ek_path + "\\tags\\" + bitmap + ".bitmap" + "\"",
                "\"" + tga_output_path.Replace("textures_output", "bitmap_xml") + "\\" + bitmap.Split('\\').Last() + ".xml" + "\""
            };

            string arguments = string.Join(" ", argumentListXML);
            tasks.Add(Task.Run(() => RunTool(tool_path, arguments, h2ek_path)));
        }

        await Task.WhenAll(tasks);
    }

    static async Task WaitForTaskCompletion(Task task)
    {
        while (task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Faulted && task.Status != TaskStatus.Canceled)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    static string[] TGAToTIF(string tga_output_path, string bitmaps_dir, string h3ek_path)
    {
        List<string> error_files = new List<string>();
        string[] tga_files = Directory.GetFiles(tga_output_path, "*.tga");
        foreach (string tga_file in tga_files)
        {
            string tif_output_path = (tga_file.Replace("_00_00", "")).Replace(".tga", ".tif");

            using (var image = new MagickImage(tga_file))
            {
                image.Format = MagickFormat.Tif;
                image.Write(tif_output_path);
            }
        }

        string[] tifFiles = Directory.GetFiles(tga_output_path, "*.tif");

        // Delete TGA files
        foreach (string tgaFile in tga_files)
        {
            File.Delete(tgaFile);
        }

        // Move TIF files to scenario bitmaps directory
        if (!Directory.Exists(bitmaps_dir))
        {
            Directory.CreateDirectory(bitmaps_dir);
        }

        foreach (string tifFile in tifFiles)
        {
            string file_name = tifFile.Split('\\').Last();
            try
            {
                File.Move(tifFile, bitmaps_dir + "\\" + file_name);
            }
            catch (IOException)
            {
                // File already exists, dont worry about it
            }
        }

        Console.WriteLine("Importing bitmaps...");
        string tool_path = h3ek_path + @"\tool.exe";
        List<string> argumentList = new List<string>
        {
            "bitmaps",
            bitmaps_dir.Split(new[] { "\\data\\" }, StringSplitOptions.None).LastOrDefault()
        };

        string arguments = string.Join(" ", argumentList);
        RunTool(tool_path, arguments, h3ek_path);

        Console.WriteLine("Setting bitmap options...");
        List<BitmapData> all_bitmap_data = new List<BitmapData>();
        string[] xml_files = Directory.GetFiles(tga_output_path.Replace("textures_output", "bitmap_xml"), "*.xml");

        foreach (string xml_file in xml_files)
        {
            XmlDocument bitmap_file = new XmlDocument();
            bitmap_file.Load(xml_file);
            XmlNode root = bitmap_file.DocumentElement;
            string bitmap_name = (new DirectoryInfo(xml_file).Name).Replace(".xml", "");
            string usage = root.SelectSingleNode("./field[@name='usage']").InnerText.Trim();
            string compression = root.SelectSingleNode("./field[@name='format']").InnerText.Trim();
            string fade_factor = root.SelectSingleNode("./field[@name='detail fade factor']").InnerText.Trim();
            string bump_height = root.SelectSingleNode("./field[@name='bump height']").InnerText.Trim();

            all_bitmap_data.Add(new BitmapData
            {
                bitmap = bitmap_name,
                type = usage,
                compr = compression,
                fade = fade_factor,
                bmp_hgt = bump_height
            });
        }

        foreach (BitmapData bitmap_data in all_bitmap_data)
        {
            string bitmap_file_path = (bitmaps_dir.Replace("data", "tags")).Split(new[] { "\\tags\\" }, StringSplitOptions.None).Last() + "\\" + bitmap_data.bitmap;
            TagPath tag_path = TagPath.FromPathAndType(bitmap_file_path, "bitm*");

            try
            {
                using (TagFile tagFile = new TagFile(tag_path))
                {
                    // Usage
                    var type = (TagFieldEnum)tagFile.SelectField("LongEnum:Usage");
                    if (bitmap_data.type.Contains("default"))
                    {
                        type.Value = 0;
                    }
                    else if (bitmap_data.type.Contains("height"))
                    {
                        type.Value = 2;
                    }
                    else if (bitmap_data.type.Contains("detail"))
                    {
                        type.Value = 4;
                    }

                    // Compression
                    var compr = (TagFieldEnum)tagFile.SelectField("ShortEnum:force bitmap format");
                    if (bitmap_data.type.Contains("height"))
                    {
                        compr.Value = 3; // Best compressed bump
                    }
                    else if (bitmap_data.compr.Contains("color-key"))
                    {
                        compr.Value = 13; //DXT1
                    }
                    else if (bitmap_data.compr.Contains("explicit alpha"))
                    {
                        compr.Value = 14; //DXT3
                    }
                    else if (bitmap_data.compr.Contains("interpolated alpha"))
                    {
                        compr.Value = 15; //DXT5
                    }

                    // Curve mode - always set force pretty
                    var curve = (TagFieldEnum)tagFile.SelectField("CharEnum:curve mode");
                    curve.Value = 2; // force pretty

                    // Fade factor
                    var fade = (TagFieldElementSingle)tagFile.SelectField("RealFraction:fade factor");
                    fade.Data = float.Parse(bitmap_data.fade);

                    // Bump height
                    if (bitmap_data.type.Contains("height"))
                    {
                        var height = (TagFieldElementSingle)tagFile.SelectField("Real:bump map height");
                        height.Data = float.Parse(bitmap_data.bmp_hgt);
                    }

                    tagFile.Save();
                }
            }
            catch (Bungie.Tags.TagLoadException)
            {
                error_files.Add($"There was an issue loading the bitmap {bitmap_file_path}. It may not have been exported from H2 correctly.");
            }
            
        }

        // Run import again to update bitmaps
        Console.WriteLine("Reimport bitmaps...");
        RunTool(tool_path, arguments, h3ek_path);
        return error_files.ToArray();
    }

    static void AddShaderScaleFunc(TagFile tagFile, int type, int index, byte byte1, byte byte2, int anim_index)
    {
        // Add scale element
        ((TagFieldBlock)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{index}]/Block:animated parameters")).AddElement();

        var func_name = (TagFieldEnum)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{index}]/Block:animated parameters[{anim_index}]/LongEnum:type");
        func_name.Value = type; // 2 is scale uniform, 3 is scale x, 4 is scale y

        var func_data = (TagFieldData)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{index}]/Block:animated parameters[{anim_index}]/Struct:function[0]/Data:data");
        byte[] data_array = new byte[32];
        data_array[0] = 1; // Seems to set the "basic" type
        data_array[6] = byte2; //byte2, unsigned
        data_array[7] = byte1; // byte1
        func_data.SetData(data_array);
    }

    static void MakeShaderTags(List<Shader> all_shader_data, string bitmaps_dir, string h3ek_path)
    {
        string bitmap_tags_dir = bitmaps_dir.Replace("data", "tags").Split(new[] { "\\tags\\" }, StringSplitOptions.None).LastOrDefault();
        string shaders_dir = (bitmaps_dir.Split(new[] { "\\data\\" }, StringSplitOptions.None).LastOrDefault()).Replace("bitmaps", "shaders");

        foreach (Shader shader in all_shader_data)
        {
            if(!shader.glob_mat.Contains("soft_organic_plant"))
            {
                string shader_name = Path.Combine(shaders_dir, shader.name);
                var tag_path = TagPath.FromPathAndType(shader_name, "rmsh*");

                // Create the tag
                TagFile tagFile = new TagFile();
                tagFile.New(tag_path);

                // Set bump on
                var bump_option = (TagFieldElementInteger)tagFile.SelectField("Struct:render_method[0]/Block:options[1]/ShortInteger:short");
                bump_option.Data = 1; // 1 for standard bump

                // Blend mode?
                if (shader.template.Contains("opaque\\overlay"))
                {
                    // Set blend mode to double multiply
                    var blend_option = (TagFieldElementInteger)tagFile.SelectField("Struct:render_method[0]/Block:options[7]/ShortInteger:short");
                    blend_option.Data = 4;
                }

                // Global material
                var global_mat = (TagFieldElementStringID)tagFile.SelectField("StringID:material name");
                global_mat.Data = shader.glob_mat;

                int param_index = 0;

                foreach (Parameter param in shader.parameters)
                {
                    if (param.name == "base_map")
                    {
                        string bitmap_filename = new DirectoryInfo(param.bitmap).Name;
                        string base_map_path = Path.Combine(bitmap_tags_dir, bitmap_filename);

                        // Add base map parameter
                        ((TagFieldBlock)tagFile.SelectField("Struct:render_method[0]/Block:parameters")).AddElement();
                        var param_name = (TagFieldElementStringID)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/StringID:parameter name");
                        param_name.Data = "base_map";
                        var param_type = (TagFieldEnum)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/LongEnum:parameter type");
                        param_type.Value = 0;

                        // Set base map
                        var base_map = (TagFieldReference)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/Reference:bitmap");
                        base_map.Path = TagPath.FromPathAndType(base_map_path, "bitm*");

                        // Set aniso
                        var flags = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap flags");
                        flags.Data = 1;
                        var aniso = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap filter mode");
                        aniso.Data = 6;

                        // Scale function data
                        byte byte1_x = param.scalex_2;
                        byte byte2_x = (byte)(256 + param.scalex_1); // Convert to unsigned
                        byte byte1_y = param.scaley_2;
                        byte byte2_y = (byte)(256 + param.scaley_1); // Convert to unsigned
                        byte[] scales = new byte[] { byte1_x, byte2_x, byte1_y, byte2_y };
                        bool all_zero = true;

                        foreach (byte scale in scales)
                        {
                            if (scale != 0)
                            {
                                all_zero = false;
                                break;
                            }
                        }

                        if (!all_zero) // No need to bother if scale values arent provided
                        {
                            if ((byte1_x == byte1_y) && (byte2_x == byte2_y)) // Uniform scale check
                            {
                                AddShaderScaleFunc(tagFile, 2, param_index, byte1_x, byte2_x, 0);
                            }
                            else // Scale is non-uniform, handle separately
                            {
                                AddShaderScaleFunc(tagFile, 3, param_index, byte1_x, byte2_x, 0);
                                AddShaderScaleFunc(tagFile, 4, param_index, byte1_y, byte2_y, 1);
                            }
                        }

                        param_index++;
                    }

                    if (param.name == "detail_map")
                    {
                        string bitmap_filename = new DirectoryInfo(param.bitmap).Name;
                        string detail_map_path = Path.Combine(bitmap_tags_dir, bitmap_filename);

                        // Add detail map parameter
                        ((TagFieldBlock)tagFile.SelectField("Struct:render_method[0]/Block:parameters")).AddElement();
                        var param_name = (TagFieldElementStringID)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/StringID:parameter name");
                        param_name.Data = "detail_map";
                        var param_type = (TagFieldEnum)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/LongEnum:parameter type");
                        param_type.Value = 0;

                        // Set detail map
                        var detail_map = (TagFieldReference)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/Reference:bitmap");
                        detail_map.Path = TagPath.FromPathAndType(detail_map_path, "bitm*");

                        // Set aniso
                        var flags = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap flags");
                        flags.Data = 1;
                        var aniso = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap filter mode");
                        aniso.Data = 6;

                        // Scale function data
                        byte byte1_x = param.scalex_2;
                        byte byte2_x = (byte)(256 + param.scalex_1); // Convert to unsigned
                        byte byte1_y = param.scaley_2;
                        byte byte2_y = (byte)(256 + param.scaley_1); // Convert to unsigned
                        byte[] scales = new byte[] { byte1_x, byte2_x, byte1_y, byte2_y };
                        bool all_zero = true;

                        foreach (byte scale in scales)
                        {
                            if (scale != 0)
                            {
                                all_zero = false;
                                break;
                            }
                        }

                        if (!all_zero) // No need to bother if scale values arent provided
                        {
                            if ((byte1_x == byte1_y) && (byte2_x == byte2_y)) // Uniform scale check
                            {
                                AddShaderScaleFunc(tagFile, 2, param_index, byte1_x, byte2_x, 0);
                            }
                            else // Scale is non-uniform, handle separately
                            {
                                AddShaderScaleFunc(tagFile, 3, param_index, byte1_x, byte2_x, 0);
                                AddShaderScaleFunc(tagFile, 4, param_index, byte1_y, byte2_y, 1);
                            }
                        }

                        param_index++;
                    }

                    if (param.name == "secondary_detail_map")
                    {
                        // Set two detail
                        var albedo_option = (TagFieldElementInteger)tagFile.SelectField("Struct:render_method[0]/Block:options[0]/ShortInteger:short");
                        albedo_option.Data = 1; // 7 for detail blend

                        string bitmap_filename = new DirectoryInfo(param.bitmap).Name;
                        string sec_detail_map_path = Path.Combine(bitmap_tags_dir, bitmap_filename);

                        // Add detail map parameter
                        ((TagFieldBlock)tagFile.SelectField("Struct:render_method[0]/Block:parameters")).AddElement();
                        var param_name = (TagFieldElementStringID)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/StringID:parameter name");
                        param_name.Data = "detail_map2";
                        var param_type = (TagFieldEnum)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/LongEnum:parameter type");
                        param_type.Value = 0;

                        // Set detail map
                        var detail2_map = (TagFieldReference)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/Reference:bitmap");
                        detail2_map.Path = TagPath.FromPathAndType(sec_detail_map_path, "bitm*");

                        // Set aniso
                        var flags = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap flags");
                        flags.Data = 1;
                        var aniso = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap filter mode");
                        aniso.Data = 6;

                        // Scale function data
                        byte byte1_x = param.scalex_2;
                        byte byte2_x = (byte)(256 + param.scalex_1); // Convert to unsigned
                        byte byte1_y = param.scaley_2;
                        byte byte2_y = (byte)(256 + param.scaley_1); // Convert to unsigned
                        byte[] scales = new byte[] { byte1_x, byte2_x, byte1_y, byte2_y };
                        bool all_zero = true;

                        foreach (byte scale in scales)
                        {
                            if (scale != 0)
                            {
                                all_zero = false;
                                break;
                            }
                        }

                        if (!all_zero) // No need to bother if scale values arent provided
                        {
                            if ((byte1_x == byte1_y) && (byte2_x == byte2_y)) // Uniform scale check
                            {
                                AddShaderScaleFunc(tagFile, 2, param_index, byte1_x, byte2_x, 0);
                            }
                            else // Scale is non-uniform, handle separately
                            {
                                AddShaderScaleFunc(tagFile, 3, param_index, byte1_x, byte2_x, 0);
                                AddShaderScaleFunc(tagFile, 4, param_index, byte1_y, byte2_y, 1);
                            }
                        }

                        param_index++;
                    }

                    if (param.name == "bump_map")
                    {
                        string bitmap_filename = new DirectoryInfo(param.bitmap).Name;
                        string bump_map_path = Path.Combine(bitmap_tags_dir, bitmap_filename);

                        // Add bump map parameter
                        ((TagFieldBlock)tagFile.SelectField("Struct:render_method[0]/Block:parameters")).AddElement();
                        var param_name = (TagFieldElementStringID)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/StringID:parameter name");
                        param_name.Data = "bump_map";
                        var param_type = (TagFieldEnum)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/LongEnum:parameter type");
                        param_type.Value = 0;

                        // Set base map
                        var bump_map = (TagFieldReference)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/Reference:bitmap");
                        bump_map.Path = TagPath.FromPathAndType(bump_map_path, "bitm*");

                        // Set aniso
                        var flags = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap flags");
                        flags.Data = 1;
                        var aniso = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap filter mode");
                        aniso.Data = 6;

                        // Scale function data
                        byte byte1_x = param.scalex_2;
                        byte byte2_x = (byte)(256 + param.scalex_1); // Convert to unsigned
                        byte byte1_y = param.scaley_2;
                        byte byte2_y = (byte)(256 + param.scaley_1); // Convert to unsigned
                        byte[] scales = new byte[] { byte1_x, byte2_x, byte1_y, byte2_y };
                        bool all_zero = true;

                        foreach (byte scale in scales)
                        {
                            if (scale != 0)
                            {
                                all_zero = false;
                                break;
                            }
                        }

                        if (!all_zero) // No need to bother if scale values arent provided
                        {
                            if ((byte1_x == byte1_y) && (byte2_x == byte2_y)) // Uniform scale check
                            {
                                AddShaderScaleFunc(tagFile, 2, param_index, byte1_x, byte2_x, 0);
                            }
                            else // Scale is non-uniform, handle separately
                            {
                                AddShaderScaleFunc(tagFile, 3, param_index, byte1_x, byte2_x, 0);
                                AddShaderScaleFunc(tagFile, 4, param_index, byte1_y, byte2_y, 1);
                            }
                        }


                        //tagFile.Save();

                        param_index++;
                    }

                    if (param.name == "alpha_test_map")
                    {
                        // Enable alpha test
                        var albedo_option = (TagFieldElementInteger)tagFile.SelectField("Struct:render_method[0]/Block:options[2]/ShortInteger:short");
                        albedo_option.Data = 1;

                        string bitmap_filename = new DirectoryInfo(param.bitmap).Name;
                        string alpha_map_path = Path.Combine(bitmap_tags_dir, bitmap_filename);

                        // Reimport diffuse map as DXT5 to make sure the alpha works
                        TagPath bitmap_path = TagPath.FromPathAndType(alpha_map_path, "bitm*");

                        using (var bitmapFile = new TagFile(bitmap_path))
                        {
                            var compr = (TagFieldEnum)bitmapFile.SelectField("ShortEnum:force bitmap format");
                            compr.Value = 15; // 15 for DXT5
                            bitmapFile.Save();
                        }

                        List<string> argumentList = new List<string>
                        {
                            "reimport-bitmaps-single",
                            alpha_map_path
                        };

                        string arguments = string.Join(" ", argumentList);
                        string tool_path = h3ek_path + @"\tool.exe";

                        Console.WriteLine($"Reimporting bitmap {bitmap_filename} as DXT5 to make sure alpha works...");
                        RunTool(tool_path, arguments, h3ek_path);

                        // Add alpha test map parameter
                        ((TagFieldBlock)tagFile.SelectField("Struct:render_method[0]/Block:parameters")).AddElement();
                        var param_name = (TagFieldElementStringID)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/StringID:parameter name");
                        param_name.Data = "alpha_test_map";
                        var param_type = (TagFieldEnum)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/LongEnum:parameter type");
                        param_type.Value = 0;

                        // Set alpha test map
                        var alpha_map = (TagFieldReference)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/Reference:bitmap");
                        alpha_map.Path = TagPath.FromPathAndType(alpha_map_path, "bitm*");

                        // Set aniso
                        var flags = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap flags");
                        flags.Data = 1;
                        var aniso = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap filter mode");
                        aniso.Data = 6;

                        // Scale function data
                        byte byte1_x = param.scalex_2;
                        byte byte2_x = (byte)(256 + param.scalex_1); // Convert to unsigned
                        byte byte1_y = param.scaley_2;
                        byte byte2_y = (byte)(256 + param.scaley_1); // Convert to unsigned
                        byte[] scales = new byte[] { byte1_x, byte2_x, byte1_y, byte2_y };
                        bool all_zero = true;

                        foreach (byte scale in scales)
                        {
                            if (scale != 0)
                            {
                                all_zero = false;
                                break;
                            }
                        }

                        if (!all_zero) // No need to bother if scale values arent provided
                        {
                            if ((byte1_x == byte1_y) && (byte2_x == byte2_y)) // Uniform scale check
                            {
                                AddShaderScaleFunc(tagFile, 2, param_index, byte1_x, byte2_x, 0);
                            }
                            else // Scale is non-uniform, handle separately
                            {
                                AddShaderScaleFunc(tagFile, 3, param_index, byte1_x, byte2_x, 0);
                                AddShaderScaleFunc(tagFile, 4, param_index, byte1_y, byte2_y, 1);
                            }
                        }

                        param_index++;
                    }

                    if (param.name == "self_illum_map")
                    {
                        string bitmap_filename = new DirectoryInfo(param.bitmap).Name;
                        string illum_map_path = Path.Combine(bitmap_tags_dir, bitmap_filename);

                        // Enable self-illum
                        var illum_option = (TagFieldElementInteger)tagFile.SelectField("Struct:render_method[0]/Block:options[6]/ShortInteger:short");
                        illum_option.Data = 1;

                        // Add illum map parameter
                        ((TagFieldBlock)tagFile.SelectField("Struct:render_method[0]/Block:parameters")).AddElement();
                        var param_name = (TagFieldElementStringID)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/StringID:parameter name");
                        param_name.Data = "self_illum_map";
                        var param_type = (TagFieldEnum)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/LongEnum:parameter type");
                        param_type.Value = 0;

                        // Set illum map
                        var illum_map = (TagFieldReference)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/Reference:bitmap");
                        illum_map.Path = TagPath.FromPathAndType(illum_map_path, "bitm*");

                        // Set aniso
                        var flags = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap flags");
                        flags.Data = 1;
                        var aniso = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap filter mode");
                        aniso.Data = 6;

                        // Scale function data
                        byte byte1_x = param.scalex_2;
                        byte byte2_x = (byte)(256 + param.scalex_1); // Convert to unsigned
                        byte byte1_y = param.scaley_2;
                        byte byte2_y = (byte)(256 + param.scaley_1); // Convert to unsigned
                        byte[] scales = new byte[] { byte1_x, byte2_x, byte1_y, byte2_y };
                        bool all_zero = true;

                        foreach (byte scale in scales)
                        {
                            if (scale != 0)
                            {
                                all_zero = false;
                                break;
                            }
                        }

                        if (!all_zero) // No need to bother if scale values arent provided
                        {
                            if ((byte1_x == byte1_y) && (byte2_x == byte2_y)) // Uniform scale check
                            {
                                AddShaderScaleFunc(tagFile, 2, param_index, byte1_x, byte2_x, 0);
                            }
                            else // Scale is non-uniform, handle separately
                            {
                                AddShaderScaleFunc(tagFile, 3, param_index, byte1_x, byte2_x, 0);
                                AddShaderScaleFunc(tagFile, 4, param_index, byte1_y, byte2_y, 1);
                            }
                        }


                        //tagFile.Save();

                        param_index++;
                    }
                }
                tagFile.Save();
            }
            else // Needs to be .shader_foliage
            {
                string shader_name = Path.Combine(shaders_dir, shader.name);
                var tag_path = TagPath.FromPathAndType(shader_name, "rmfl*");

                // Create the tag
                TagFile tagFile = new TagFile();
                tagFile.New(tag_path);

                // Set alpha test to simple
                var alpha_test = (TagFieldElementInteger)tagFile.SelectField("Struct:render_method[0]/Block:options[1]/ShortInteger:short");
                alpha_test.Data = 1;

                // Global material
                var global_mat = (TagFieldElementStringID)tagFile.SelectField("StringID:material name");
                global_mat.Data = shader.glob_mat;

                int param_index = 0;

                foreach (Parameter param in shader.parameters)
                {
                    if (param.name == "base_map")
                    {
                        string bitmap_filename = new DirectoryInfo(param.bitmap).Name;
                        string base_map_path = Path.Combine(bitmap_tags_dir, bitmap_filename);

                        // Add base map parameter
                        ((TagFieldBlock)tagFile.SelectField("Struct:render_method[0]/Block:parameters")).AddElement();
                        var param_name = (TagFieldElementStringID)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/StringID:parameter name");
                        param_name.Data = "base_map";
                        var param_type = (TagFieldEnum)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/LongEnum:parameter type");
                        param_type.Value = 0;

                        // Set base map
                        var base_map = (TagFieldReference)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/Reference:bitmap");
                        base_map.Path = TagPath.FromPathAndType(base_map_path, "bitm*");

                        // Set aniso
                        var flags = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap flags");
                        flags.Data = 1;
                        var aniso = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap filter mode");
                        aniso.Data = 6;

                        // Scale function data
                        byte byte1_x = param.scalex_2;
                        byte byte2_x = (byte)(256 + param.scalex_1); // Convert to unsigned
                        byte byte1_y = param.scaley_2;
                        byte byte2_y = (byte)(256 + param.scaley_1); // Convert to unsigned
                        byte[] scales = new byte[] { byte1_x, byte2_x, byte1_y, byte2_y };
                        bool all_zero = true;

                        foreach (byte scale in scales)
                        {
                            if (scale != 0)
                            {
                                all_zero = false;
                                break;
                            }
                        }

                        if (!all_zero) // No need to bother if scale values arent provided
                        {
                            if ((byte1_x == byte1_y) && (byte2_x == byte2_y)) // Uniform scale check
                            {
                                AddShaderScaleFunc(tagFile, 2, param_index, byte1_x, byte2_x, 0);
                            }
                            else // Scale is non-uniform, handle separately
                            {
                                AddShaderScaleFunc(tagFile, 3, param_index, byte1_x, byte2_x, 0);
                                AddShaderScaleFunc(tagFile, 4, param_index, byte1_y, byte2_y, 1);
                            }
                        }

                        param_index++;
                    }

                    if (param.name == "detail_map")
                    {
                        string bitmap_filename = new DirectoryInfo(param.bitmap).Name;
                        string detail_map_path = Path.Combine(bitmap_tags_dir, bitmap_filename);

                        // Add detail map parameter
                        ((TagFieldBlock)tagFile.SelectField("Struct:render_method[0]/Block:parameters")).AddElement();
                        var param_name = (TagFieldElementStringID)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/StringID:parameter name");
                        param_name.Data = "detail_map";
                        var param_type = (TagFieldEnum)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/LongEnum:parameter type");
                        param_type.Value = 0;

                        // Set detail map
                        var detail_map = (TagFieldReference)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/Reference:bitmap");
                        detail_map.Path = TagPath.FromPathAndType(detail_map_path, "bitm*");

                        // Set aniso
                        var flags = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap flags");
                        flags.Data = 1;
                        var aniso = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap filter mode");
                        aniso.Data = 6;

                        // Scale function data
                        byte byte1_x = param.scalex_2;
                        byte byte2_x = (byte)(256 + param.scalex_1); // Convert to unsigned
                        byte byte1_y = param.scaley_2;
                        byte byte2_y = (byte)(256 + param.scaley_1); // Convert to unsigned
                        byte[] scales = new byte[] { byte1_x, byte2_x, byte1_y, byte2_y };
                        bool all_zero = true;

                        foreach (byte scale in scales)
                        {
                            if (scale != 0)
                            {
                                all_zero = false;
                                break;
                            }
                        }

                        if (!all_zero) // No need to bother if scale values arent provided
                        {
                            if ((byte1_x == byte1_y) && (byte2_x == byte2_y)) // Uniform scale check
                            {
                                AddShaderScaleFunc(tagFile, 2, param_index, byte1_x, byte2_x, 0);
                            }
                            else // Scale is non-uniform, handle separately
                            {
                                AddShaderScaleFunc(tagFile, 3, param_index, byte1_x, byte2_x, 0);
                                AddShaderScaleFunc(tagFile, 4, param_index, byte1_y, byte2_y, 1);
                            }
                        }

                        param_index++;
                    }

                    if (param.name == "bump_map")
                    {
                        string bitmap_filename = new DirectoryInfo(param.bitmap).Name;
                        string alpha_map_path = Path.Combine(bitmap_tags_dir, bitmap_filename);

                        // Reimport bump map as colour map to get alpha test working
                        TagPath bitmap_path = TagPath.FromPathAndType(alpha_map_path, "bitm*");

                        using (var bitmapFile = new TagFile(bitmap_path))
                        {
                            var compr = (TagFieldEnum)bitmapFile.SelectField("ShortEnum:force bitmap format");
                            compr.Value = 2;
                            bitmapFile.Save();
                        }

                        List<string> argumentList = new List<string>
                        {
                            "reimport-bitmaps-single",
                            alpha_map_path
                        };

                        string arguments = string.Join(" ", argumentList);
                        string tool_path = h3ek_path + @"\tool.exe";

                        Console.WriteLine($"Reimporting bitmap {bitmap_filename} as colour for shader foliage...");
                        RunTool(tool_path, arguments, h3ek_path);

                        // Add alpha map parameter
                        ((TagFieldBlock)tagFile.SelectField("Struct:render_method[0]/Block:parameters")).AddElement();
                        var param_name = (TagFieldElementStringID)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/StringID:parameter name");
                        param_name.Data = "alpha_test_map";
                        var param_type = (TagFieldEnum)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/LongEnum:parameter type");
                        param_type.Value = 0;

                        // Set alpha map
                        var bump_map = (TagFieldReference)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/Reference:bitmap");
                        bump_map.Path = TagPath.FromPathAndType(alpha_map_path, "bitm*");

                        // Set aniso
                        var flags = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap flags");
                        flags.Data = 1;
                        var aniso = (TagFieldElementInteger)tagFile.SelectField($"Struct:render_method[0]/Block:parameters[{param_index}]/ShortInteger:bitmap filter mode");
                        aniso.Data = 6;

                        // Scale function data
                        byte byte1_x = param.scalex_2;
                        byte byte2_x = (byte)(256 + param.scalex_1); // Convert to unsigned
                        byte byte1_y = param.scaley_2;
                        byte byte2_y = (byte)(256 + param.scaley_1); // Convert to unsigned
                        byte[] scales = new byte[] { byte1_x, byte2_x, byte1_y, byte2_y };
                        bool all_zero = true;

                        foreach (byte scale in scales)
                        {
                            if (scale != 0)
                            {
                                all_zero = false;
                                break;
                            }
                        }

                        if (!all_zero) // No need to bother if scale values arent provided
                        {
                            if ((byte1_x == byte1_y) && (byte2_x == byte2_y)) // Uniform scale check
                            {
                                AddShaderScaleFunc(tagFile, 2, param_index, byte1_x, byte2_x, 0);
                            }
                            else // Scale is non-uniform, handle separately
                            {
                                AddShaderScaleFunc(tagFile, 3, param_index, byte1_x, byte2_x, 0);
                                AddShaderScaleFunc(tagFile, 4, param_index, byte1_y, byte2_y, 1);
                            }
                        }


                        //tagFile.Save();

                        param_index++;
                    }
                }
                tagFile.Save();
            }
        }
    }
}