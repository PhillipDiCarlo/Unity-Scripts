# Unity Scripts by Italiandogs

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

---

## Usage
1. Place these scripts in an `Editor` folder within your Unity project.
2. Open Unity, and the new tools will be available under **Tools > Italiandogs** in the menu bar.
3. Select the appropriate tool for your needs, follow the UI prompts, and check the Unity Console for any log messages regarding changes made.

## License
This project is licensed under the MIT License. Feel free to modify and use these scripts as needed.

---

Happy optimizing!
