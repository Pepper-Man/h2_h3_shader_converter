# H2 to H3 Shader Converter
A C# program for converting H2 shader tags into H3 format. This is primarily built for use by the porting team I am in, but feel free to use it for you own ports. As of right now, this only supports BSP data, not objects.

# Requirements
* Requires [.NET 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)

# Features
* Extracts all required bitmaps
* Imports all bitmaps into H3 with correct settings (usage, compression, bump height etc)
* Creates all shaders referenced in the given BSP(s)
* Can handle special plasma shaders
* Can handle basic shader foliage for trees and bushes
* Within each shader, this program automatically sets (if required):
    * Base map
    * Standard bump mapping
    * Detail map(s) (handles single, two-detail/detail_blend and three-detail)
    * Self illum map
    * Anisotropic filtering set to 4x on all bitmaps
    * Specular mask from diffuse
    * Uniform bitmap scaling, or separate x and y scaling (if required)
    * Cook torrance, with different settings applied based on the H2 shader's specular type
    * Specular, fresnel and env tint colours
    * Alpha test/blend
    * Dynamic environment mapping

# Usage
* Download the latest release, or compile your own version.
* Extract H2 bsp to XML with `tool export-tag-to-xml`. E.g. `tool export-tag-to-xml "C:\Program Files (x86)\Steam\steamapps\common\H2EK\tags\scenarios\solo\03a_oldmombasa\earthcity_1.scenario_structure_bsp" "C:\Program Files (x86)\Steam\steamapps\common\H2EK\tags\scenarios\solo\03a_oldmombasa\earthcity_1.xml"`
* I cannot distribute the required ManagedBlam.dll, so you will need to either:
    * Copy your Halo 3 ManagedBlam.dll (found in "H3EK\bin") into the same folder as this exe
    * Alternatively, simply place the files of this program directly into your "H3EK\bin" directory.
* Run this .exe, provide the file paths when prompted.

# Notes

* There will very likely be bugs and issues that I haven't caught. Please let me know on Discord - `pepperman`