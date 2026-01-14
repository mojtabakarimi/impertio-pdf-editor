# PDF Viewer - Cross-Platform PDF Reader

A high-performance, cross-platform PDF viewer built with .NET 10 and Avalonia UI. This application provides fast PDF rendering with built-in caching and will scale into a full-featured PDF editor.

## Features

### Current Features (v1.0)
- Fast PDF rendering using PDFium
- Page navigation (Previous/Next)
- Zoom controls (In/Out/Reset)
- Page caching for improved performance
- Cross-platform support (Windows, macOS, Linux)
- Clean, modern UI with menu and toolbar
- Keyboard shortcuts (Ctrl+O, Ctrl++, Ctrl+-, Ctrl+0)
- Status bar with page information and zoom level

### Planned Features (Future Versions)
- Thumbnail sidebar
- Text search within documents
- Page rotation
- Full-screen mode
- Text selection and copy
- Annotations and highlights
- Form filling
- Digital signatures
- Page manipulation (add, remove, reorder)
- Text editing
- Image insertion
- PDF split/merge

## Technology Stack

- **Framework**: .NET 10 (LTS)
- **UI Framework**: Avalonia UI 11.3+
- **PDF Rendering**: PDFium (via PdfiumViewer)
- **Architecture**: MVVM + Clean Architecture
- **Graphics**: SkiaSharp

## Project Structure

```
PdfViewer/
├── src/
│   ├── PdfViewer.Desktop/          # Main Avalonia application
│   │   ├── Views/                   # UI Views (XAML)
│   │   ├── ViewModels/              # ViewModels (MVVM)
│   │   └── Assets/                  # Icons and resources
│   │
│   ├── PdfViewer.Core/              # Core business logic
│   │   ├── Services/                # Document services
│   │   ├── Models/                  # Domain models
│   │   └── Interfaces/              # Service interfaces
│   │
│   └── PdfViewer.Rendering/         # PDF rendering engine
│       ├── PdfiumRenderService.cs   # PDFium wrapper
│       ├── PageCache.cs             # LRU page cache
│       ├── RenderQueue.cs           # Async render queue
│       └── ThumbnailGenerator.cs    # Thumbnail generation
│
└── PdfViewer.sln                    # Solution file
```

## Requirements

- **.NET 10 SDK** or later
- **Windows 10/11**, **macOS 10.15+**, or **Linux** (Ubuntu 20.04+, Fedora 36+, etc.)
- **4 GB RAM** minimum (8 GB recommended)
- **100 MB** disk space

## Building the Application

### 1. Clone or Download the Project

```bash
cd "C:\Users\ASUS\Dropbox (Personal)\Job\3BM\Cluade AI"
```

### 2. Restore Dependencies

```bash
dotnet restore PdfViewer.sln
```

### 3. Build the Solution

```bash
dotnet build PdfViewer.sln --configuration Release
```

## Running the Application

### From Command Line

```bash
dotnet run --project src/PdfViewer.Desktop/PdfViewer.Desktop.csproj
```

### From Visual Studio / Visual Studio Code

1. Open `PdfViewer.sln`
2. Set `PdfViewer.Desktop` as the startup project
3. Press F5 to run

### From Published Binary

```bash
# Publish for your platform
dotnet publish src/PdfViewer.Desktop/PdfViewer.Desktop.csproj -c Release -o ./publish

# Run the published app
./publish/PdfViewer.Desktop.exe   # Windows
./publish/PdfViewer.Desktop        # Linux/macOS
```

## Usage

### Opening a PDF

1. Click **File → Open** or press **Ctrl+O**
2. Select a PDF file from the file dialog
3. The document will load and display the first page

### Navigation

- **Previous Page**: Click "◀ Previous" button or use keyboard shortcut
- **Next Page**: Click "Next ▶" button or use keyboard shortcut
- **Page Display**: Current page and total pages shown in toolbar

### Zoom Controls

- **Zoom In**: Click "🔍+" button or press **Ctrl+Plus**
- **Zoom Out**: Click "🔍-" button or press **Ctrl+Minus**
- **Reset Zoom**: Click "100%" button or press **Ctrl+0**
- **Current Zoom**: Displayed as percentage in toolbar

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open PDF file |
| Ctrl+Plus (+) | Zoom in |
| Ctrl+Minus (-) | Zoom out |
| Ctrl+0 | Reset zoom to 100% |

## Performance Features

### Page Caching

The application uses an LRU (Least Recently Used) cache to store rendered pages in memory:
- Default cache size: 50 pages
- Automatic cache eviction when full
- Significantly improves performance when navigating back to previously viewed pages

### Lazy Loading

Pages are rendered on-demand:
- Only the currently visible page is rendered initially
- Adjacent pages can be pre-rendered in the background (future enhancement)

### Async Rendering

All PDF rendering operations are performed asynchronously:
- UI remains responsive during rendering
- Background rendering queue for smooth performance
- Cancellation token support for interrupted operations

## Architecture

### Clean Architecture Principles

The application follows clean architecture with clear separation of concerns:

1. **PdfViewer.Desktop** (Presentation Layer)
   - Avalonia UI views and view models
   - User interaction handling
   - No direct dependency on PDF rendering library

2. **PdfViewer.Core** (Domain Layer)
   - Business logic and domain models
   - Service interfaces
   - Platform-independent
   - No dependencies on UI or infrastructure

3. **PdfViewer.Rendering** (Infrastructure Layer)
   - PDF rendering implementation using PDFium
   - Caching and performance optimization
   - Can be swapped with different PDF engines

### MVVM Pattern

The UI layer uses the MVVM (Model-View-ViewModel) pattern:
- **Views**: XAML files defining the UI structure
- **ViewModels**: Handle UI logic and data binding
- **Models**: Domain entities from PdfViewer.Core
- **CommunityToolkit.Mvvm**: Provides MVVM helpers and commands

## Known Issues

### Warnings

- **NU1701**: PdfiumViewer package compatibility warning
  - This is expected as PdfiumViewer targets .NET Framework
  - Works correctly through .NET compatibility layer
  - Will be resolved when PdfiumViewer releases .NET 10 support

- **CA1416**: ImageFormat.Png platform-specific warning
  - System.Drawing.Common works on all platforms when properly configured
  - Runtime handles cross-platform compatibility automatically

## Future Enhancements

### Short-term (Next Release)

1. Pre-rendering adjacent pages for smoother scrolling
2. Thumbnail sidebar for quick navigation
3. Search functionality
4. Page rotation

### Mid-term

1. GPU-accelerated rendering (SkiaSharp GPU backend)
2. Virtual scrolling for large documents
3. Progressive rendering (low-res preview → high-res)
4. Memory pooling optimization

### Long-term (Full Editor)

1. Text selection and copying
2. Annotations and comments
3. Form filling
4. Digital signatures
5. Page manipulation (add, remove, reorder)
6. Text editing
7. Split/merge PDFs

## Contributing

This project is currently under development. Contributions and suggestions are welcome!

## License

[Specify your license here]

## Acknowledgments

- **PDFium**: Google's PDF rendering engine
- **Avalonia UI**: Cross-platform .NET UI framework
- **PdfiumViewer**: .NET wrapper for PDFium
- **.NET Team**: For the excellent .NET 10 platform
