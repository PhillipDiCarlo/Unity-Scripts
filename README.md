# Unity Scripts

This repository contains a collection of custom Unity scripts aimed at optimizing assets and managing shaders within a Unity project. These scripts add new tools to the Unity Editor to streamline common tasks. Below is a description of each script included in this repository.

---

## Scripts

### 1. FixErrorShaders.cs
This script adds a menu item in Unity under **Tools > Italiandogs > Fix Error Shaders**. When run, it searches for materials in the project using the `Hidden/InternalErrorShader` (often assigned to materials with missing shaders) and replaces them with Unity's Standard shader. This helps resolve common shader errors by reassigning a functional shader to affected materials.

- **Functionality:** Replaces missing or broken shaders with the Standard shader.
- **Usage:** Go to **Tools > Italiandogs > Fix Error Shaders** and click the "Fix Error Shaders" button.
- **Logs:** Outputs a message for each material updated.

### 2. ReduceTextureSize.cs
This script adds a menu item under **Tools > Italiandogs > Reduce Large Textures**. It identifies textures in the active scene that are 2048x2048 pixels or larger and reduces their maximum texture size to 1024x1024 pixels. This is useful for optimizing large textures, which can help reduce memory usage and improve performance.

- **Functionality:** Scans materials for large textures and reduces their size.
- **Usage:** Go to **Tools > Italiandogs > Reduce Large Textures** to execute the texture reduction.
- **Logs:** Outputs the name of each texture that has been resized.

### 3. ShaderReplacer.cs
This script introduces a menu item under **Tools > Italiandogs > Replace Shaders**. It allows users to select a source shader and a target shader, replacing all instances of the source shader with the target shader in the project's materials. This can be helpful for updating materials to use a new or optimized shader.

- **Functionality:** Replaces a specific shader with another shader in all materials using the source shader.
- **Usage:** Go to **Tools > Italiandogs > Replace Shaders**, select the source and target shaders, then click "Replace Shaders".
- **Logs:** Outputs a message for each material updated with the new shader.

### 4. ApplyMeshCompression.cs
This script adds a menu item under **Tools > Italiandogs > Apply Mesh Compression**. It locates all meshes in the active scene that currently have no compression applied and sets their mesh compression level to "Medium". Mesh compression can save storage space and reduce load times, particularly useful for complex scenes.

- **Functionality:** Applies medium compression to meshes without compression.
- **Usage:** Go to **Tools > Italiandogs > Apply Mesh Compression** to compress meshes in the scene.
- **Logs:** Outputs the name of each mesh that had compression applied.

### 5. TextureCompressionTool.cs
This script introduces a tool under **Tools > Italiandogs > Texture Compression Tool** that scans all textures in the scene and identifies those that lack compression or do not use Crunch Compression. The user can then process these textures to apply normal-quality compression and enable Crunch Compression, improving memory usage and load times.

- **Functionality:** Scans textures in the scene, identifies uncompressed textures, and applies normal-quality compression and Crunch Compression.
- **Usage:** Go to **Tools > Italiandogs > Texture Compression Tool**. The tool displays a list of textures to process. Click **Process Textures** to apply the changes. A summary window will display the number of textures processed and any failures.
- **Logs:** Outputs the name of each texture processed and lists any textures that failed to update.

### 6. LightmapScaleEditor.cs
This script adds a menu item under **Tools > Italiandogs > Lightmap Scale Editor**. It allows users to bulk adjust the "Scale In Lightmap" property for all objects in the scene that have a `Renderer` component. This property determines the amount of lightmap space allocated to an object, affecting lighting detail and texture memory usage.

- **Functionality:** Adjusts the "Scale In Lightmap" property for all game objects with renderers in the scene.
- **Usage:** Go to **Tools > Italiandogs > Lightmap Scale Editor**, input the desired scale value, and click "Apply to All Objects" to update the property for all eligible objects.
- **Logs:** Outputs a message in the Unity Console for each object updated.

### 7. ExportTerrain.cs
This script introduces a menu item under **Tools > Italiandogs > Terrain > Export To Obj...**. It allows users to export Unity terrain data into a .OBJ file, which is a standard format for 3D models. The script supports different resolutions and formats (triangles or quads) for the exported terrain.

- **Functionality:** Exports Unity terrain to a .OBJ file with customizable resolution and format.
- **Usage:**
  1. Select a terrain object in the Unity Editor.
  2. Navigate to **Terrain > Export To Obj...** in the menu bar.
  3. Adjust export settings in the pop-up window (format and resolution).
  4. Click **Export** and choose the destination file.
- **Logs:** Displays progress during export and logs any errors to the Unity Console.

---

## Usage
1. Place these scripts in an `Editor` folder within your Unity project.
2. Open Unity, and the new tools will be available under **Tools > Italiandogs** in the menu bar.
3. Select the appropriate tool for your needs, follow the UI prompts, and check the Unity Console for any log messages regarding changes made.

## License
This project is licensed under the MIT License. Feel free to modify and use these scripts as needed.

---

Happy optimizing!
