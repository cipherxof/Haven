# Avalonia3DControl

A cross-platform 3D control for Avalonia applications with OpenGL rendering support.

## Features

- **Cross-platform**: Built on Avalonia framework, supports Windows, macOS, and Linux
- **OpenGL Rendering**: High-performance 3D rendering with optimized memory allocation
- **Multiple Projection Modes**: Switch between perspective and orthographic projections
- **Independent Coordinate Systems**: 
  - **Coordinate Axes**: Configurable 3D coordinate axes with independent rendering
  - **Mini Axes**: Corner mini-axes for orientation reference with consistent design
- **Multiple Shading Modes**: Support for vertex shading and texture shading
- **Interactive Camera**: Mouse-controlled camera rotation and zoom with smooth controls
- **Robust Error Handling**: Comprehensive error management and exception handling
- **Modular Architecture**: Well-structured codebase following SOLID principles

## Screenshots

*Screenshots will be added here*

## Requirements

- .NET 10.0 or later
- OpenGL 3.3 or later
- Avalonia 11.0 or later

## Installation

1. Clone the repository:
```bash
git clone https://github.com/crazylin/Avalonia3DControl.git
cd Avalonia3DControl
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the project:
```bash
dotnet build
```

## Usage

### Basic Usage

The `OpenGL3DControl` can be integrated into any Avalonia application:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:Avalonia3DControl">
    <local:OpenGL3DControl />
</UserControl>
```

### Camera Controls

- **Mouse Drag**: Rotate the camera around the scene
- **Mouse Wheel**: Zoom in/out
- **Projection Toggle**: Switch between perspective and orthographic views

### Rendering Modes

- **Vertex Shading**: Display models with vertex colors
- **Texture Shading**: Display models with texture mapping

## Architecture

### Core Components

- **OpenGL3DControl**: Main 3D control component with integrated event handling
- **OpenGLRenderer**: Optimized OpenGL rendering engine with null-safe operations
- **Scene3D**: Enhanced 3D scene management with independent coordinate systems
- **Camera**: Advanced camera system with smooth projection controls
- **Model3D**: Flexible 3D model representation with material support
- **Material**: Comprehensive material and shading system
- **CoordinateAxes**: Independent coordinate axes component with configurable display
- **MiniAxes**: Compact orientation reference with consistent design patterns
- **ErrorHandler**: Robust error handling and exception management system

### Project Structure

```
Avalonia3DControl/
├── Core/
│   ├── Animation/        # Animation system and modal data
│   ├── Cameras/          # Camera system with controller
│   ├── ErrorHandling/    # Comprehensive error management
│   ├── Input/            # Input handling system
│   ├── Lighting/         # Lighting system
│   ├── Models/           # 3D model classes
│   ├── CoordinateAxes.cs # Independent coordinate axes component
│   ├── MiniAxes.cs       # Mini axes orientation reference
│   └── Scene3D.cs        # Enhanced scene management
├── Geometry/
│   └── Factories/        # Optimized geometry generation
├── Materials/            # Material and shading system
├── Rendering/
│   ├── OpenGL/           # Optimized OpenGL rendering engine
│   ├── GeometryRenderer.cs # Geometry rendering utilities
│   ├── RenderConfiguration.cs # Render settings
│   ├── RenderState.cs    # Render state management
│   ├── ShaderLoader.cs   # Shader loading utilities
│   └── ShaderManager.cs  # Shader program management
├── UI/                   # Enhanced user interface
│   ├── CharacterRenderer.cs # Text rendering
│   └── GradientBar.cs    # Gradient visualization
├── Shaders/              # GLSL shader programs
│   ├── GradientBar/      # Gradient rendering shaders
│   └── Renderer/         # Model rendering shaders
└── OpenGL3DControl.cs    # Main 3D control component
```

## Recent Improvements

### Version 2.0 Enhancements

- **Independent Coordinate Systems**: Extracted `CoordinateAxes` as a standalone component with consistent design patterns matching `MiniAxes`
- **Enhanced Error Handling**: Implemented comprehensive error management with null-safe operations throughout the codebase
- **Performance Optimizations**: 
  - Optimized memory allocation patterns
  - Improved rendering pipeline efficiency
  - Enhanced shader management system
- **Code Quality Improvements**:
  - Applied SOLID principles across the architecture
  - Improved code documentation with comprehensive XML comments
  - Eliminated all compiler warnings for cleaner builds
  - Enhanced modularity and maintainability
- **UI Enhancements**: Added animation support and improved user interaction feedback

## Technical Details

### OpenGL Features

- Vertex Buffer Objects (VBO)
- Element Buffer Objects (EBO)
- Vertex Array Objects (VAO)
- Shader programs (vertex and fragment shaders)
- Depth testing
- Face culling

### Coordinate System

- Right-handed coordinate system
- Y-axis pointing up
- Z-axis pointing towards the viewer
- Independent coordinate axes rendering

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Avalonia](https://avaloniaui.net/) - Cross-platform .NET UI framework
- [OpenGL](https://www.opengl.org/) - Graphics API
- [OpenTK](https://opentk.net/) - OpenGL bindings for .NET

## Contact

- Author: crazylin
- Email: crazylin@msn.com
- GitHub: [crazylin](https://github.com/crazylin)

---

*For Chinese documentation, see [README_CN.md](README_CN.md)*
