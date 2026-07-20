# Avalonia3DControl

基于Avalonia框架的跨平台3D控件，支持OpenGL渲染。

## 功能特性

- **跨平台支持**: 基于Avalonia框架构建，支持Windows、macOS和Linux
- **OpenGL渲染**: 高性能3D渲染，优化内存分配
- **多种投影模式**: 支持透视投影和正交投影切换
- **独立坐标系统**: 
  - **坐标轴**: 可配置的3D坐标轴，独立渲染
  - **迷你坐标轴**: 角落迷你坐标轴，设计模式一致的方向参考
- **多种着色模式**: 支持顶点着色和纹理着色
- **交互式相机**: 鼠标控制相机旋转和缩放，操作流畅
- **健壮错误处理**: 全面的错误管理和异常处理
- **模块化架构**: 遵循SOLID原则的良好代码结构

## 截图展示

*截图将在此处添加*

## 系统要求

- .NET 10.0 或更高版本
- OpenGL 3.3 或更高版本
- Avalonia 11.0 或更高版本

## 安装说明

1. 克隆仓库：
```bash
git clone https://github.com/crazylin/Avalonia3DControl.git
cd Avalonia3DControl
```

2. 恢复依赖项：
```bash
dotnet restore
```

3. 构建项目：
```bash
dotnet build
```

## 使用方法

### 基本用法

`OpenGL3DControl` 可以集成到任何Avalonia应用程序中：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:Avalonia3DControl">
    <local:OpenGL3DControl />
</UserControl>
```

### 相机控制

- **鼠标拖拽**: 围绕场景旋转相机
- **鼠标滚轮**: 放大/缩小
- **投影切换**: 在透视投影和正交投影之间切换

### 渲染模式

- **顶点着色**: 使用顶点颜色显示模型
- **纹理着色**: 使用纹理映射显示模型

## 架构设计

### 核心组件

- **OpenGL3DControl**: 主要的3D控件组件，集成事件处理
- **OpenGLRenderer**: 优化的OpenGL渲染引擎，支持空值安全操作
- **Scene3D**: 增强的3D场景管理，支持独立坐标系统
- **Camera**: 高级相机系统，流畅的投影控制
- **Model3D**: 灵活的3D模型表示，支持材质系统
- **Material**: 全面的材质和着色系统
- **CoordinateAxes**: 独立的坐标轴组件，可配置显示
- **MiniAxes**: 紧凑的方向参考，设计模式一致
- **ErrorHandler**: 健壮的错误处理和异常管理系统

### 项目结构

```
Avalonia3DControl/
├── Core/
│   ├── Animation/        # 动画系统和模态数据
│   ├── Cameras/          # 相机系统与控制器
│   ├── ErrorHandling/    # 全面的错误管理
│   ├── Input/            # 输入处理系统
│   ├── Lighting/         # 光照系统
│   ├── Models/           # 3D模型类
│   ├── CoordinateAxes.cs # 独立坐标轴组件
│   ├── MiniAxes.cs       # 迷你坐标轴方向参考
│   └── Scene3D.cs        # 增强的场景管理
├── Geometry/
│   └── Factories/        # 优化的几何体生成
├── Materials/            # 材质和着色系统
├── Rendering/
│   ├── OpenGL/           # 优化的OpenGL渲染引擎
│   ├── GeometryRenderer.cs # 几何体渲染工具
│   ├── RenderConfiguration.cs # 渲染设置
│   ├── RenderState.cs    # 渲染状态管理
│   ├── ShaderLoader.cs   # 着色器加载工具
│   └── ShaderManager.cs  # 着色器程序管理
├── UI/                   # 增强的用户界面
│   ├── CharacterRenderer.cs # 文本渲染
│   └── GradientBar.cs    # 梯度可视化
├── Shaders/              # GLSL着色器程序
│   ├── GradientBar/      # 梯度渲染着色器
│   └── Renderer/         # 模型渲染着色器
└── OpenGL3DControl.cs    # 主要3D控件组件
```

## 最近改进

### 版本 2.0 增强功能

- **独立坐标系统**: 将 `CoordinateAxes` 提取为独立组件，与 `MiniAxes` 保持一致的设计模式
- **增强错误处理**: 实现全面的错误管理，整个代码库支持空值安全操作
- **性能优化**: 
  - 优化内存分配模式
  - 改进渲染管道效率
  - 增强着色器管理系统
- **代码质量改进**:
  - 在架构中应用SOLID原则
  - 完善代码文档，添加全面的XML注释
  - 消除所有编译器警告，实现更清洁的构建
  - 增强模块化和可维护性
- **UI增强**: 添加动画支持，改进用户交互反馈

## 技术细节

### OpenGL特性

- 顶点缓冲对象 (VBO)
- 元素缓冲对象 (EBO)
- 顶点数组对象 (VAO)
- 着色器程序（顶点着色器和片段着色器）
- 深度测试
- 面剔除

### 坐标系统

- 右手坐标系
- Y轴向上
- Z轴指向观察者
- 独立的坐标轴渲染

## 贡献指南

欢迎贡献代码！请随时提交Pull Request。

1. Fork 本仓库
2. 创建您的功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交您的更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开一个Pull Request

## 许可证

本项目采用MIT许可证 - 详情请参阅 [LICENSE](LICENSE) 文件。

## 致谢

- [Avalonia](https://avaloniaui.net/) - 跨平台.NET UI框架
- [OpenGL](https://www.opengl.org/) - 图形API
- [OpenTK](https://opentk.net/) - .NET的OpenGL绑定

## 联系方式

- 作者: crazylin
- 邮箱: crazylin@msn.com
- GitHub: [crazylin](https://github.com/crazylin)

---

*English documentation available at [README.md](README.md)*


## 渲染模式兼容与迷你坐标轴优化（2025-01）

- 坐标轴与迷你坐标轴渲染解耦：无论全局渲染模式（面/线/点）如何切换，坐标轴与迷你坐标轴始终以 Fill 模式稳定渲染。实现方式为在渲染前临时覆盖当前渲染模式，渲染完成后恢复，确保不影响主模型渲染流程（参见 <mcfile name="OpenGLRenderer.cs" path="Rendering/OpenGL/OpenGLRenderer.cs"></mcfile> 中的 <mcsymbol name="RenderSceneWithAxes" filename="OpenGLRenderer.cs" path="Rendering/OpenGL/OpenGLRenderer.cs" startline="201" type="function"></mcsymbol> 与 <mcsymbol name="RenderMiniAxes" filename="OpenGLRenderer.cs" path="Rendering/OpenGL/OpenGLRenderer.cs" startline="1277" type="function"></mcsymbol>）。
- 迷你坐标轴 XYZ 标签改为填充字体：用三角形绘制“实心”字母（X/Y/Z），可读性更强。字母大小、笔画宽度与线宽均已调优，并使笔画宽度随尺寸自适应，保证在不同 DPI/视口下表现稳定（参见 <mcsymbol name="RenderAxisLabels" filename="OpenGLRenderer.cs" path="Rendering/OpenGL/OpenGLRenderer.cs" startline="1369" type="function"></mcsymbol>、<mcsymbol name="DrawFilledLetterX" filename="OpenGLRenderer.cs" path="Rendering/OpenGL/OpenGLRenderer.cs" startline="1419" type="function"></mcsymbol>、<mcsymbol name="DrawFilledLetterY" filename="OpenGLRenderer.cs" path="Rendering/OpenGL/OpenGLRenderer.cs" startline="1453" type="function"></mcsymbol>、<mcsymbol name="DrawFilledLetterZ" filename="OpenGLRenderer.cs" path="Rendering/OpenGL/OpenGLRenderer.cs" startline="1496" type="function"></mcsymbol>）。
- 标签位置外移：将 XYZ 标签定位在各轴尖端之外，并增加轴向偏移量，避免与轴重叠，视觉更清晰。
- 通用线框/点模式兼容：在 Windows Core Profile 下禁用直接使用 GL.PolygonMode，改为通过线宽、点大小（包含 ProgramPointSize）等手段实现近似效果，避免崩溃；并提供安全封装方法以统一处理（参见 <mcsymbol name="SetRenderMode" filename="OpenGLRenderer.cs" path="Rendering/OpenGL/OpenGLRenderer.cs" startline="763" type="function"></mcsymbol> 与 <mcsymbol name="SetPolygonModeSafe" filename="OpenGLRenderer.cs" path="Rendering/OpenGL/OpenGLRenderer.cs" startline="829" type="function"></mcsymbol>，以及文档 <mcfile name="OPENGL_FIX_README.md" path="OPENGL_FIX_README.md"></mcfile>）。
