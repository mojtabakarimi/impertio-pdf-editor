# C# WPF PDF Viewer

A modern Windows PDF viewer built with WPF and PDFium, featuring clean architecture and comprehensive testing.

## 📋 Project Overview

This is a C# .NET WPF application designed for Windows with a focus on:
- **Clean Architecture**: Separated into Core, Desktop, and Rendering layers
- **Modern UI**: WPF-based user interface
- **PDF Rendering**: Using PDFium.NET SDK for high-quality rendering
- **Testability**: Comprehensive unit test coverage

## 🏗️ Project Structure

```
csharp-pdf-viewer/
├── src/
│   ├── PdfViewer.Core/         # Core business logic and interfaces
│   ├── PdfViewer.Desktop/      # WPF UI layer
│   └── PdfViewer.Rendering/    # PDF rendering implementation
├── tests/                      # Unit tests
├── docs/                       # Project documentation
│   ├── PROJECT_SUMMARY.md      # Architecture and goals
│   ├── SESSION_LOG.md          # Development logs
│   └── TESTING_GUIDE.md        # Testing procedures
└── PdfViewer.sln              # Visual Studio solution
```

## 🔧 Technologies

- **.NET 6.0+** - Framework
- **WPF** - Windows Presentation Foundation for UI
- **PDFium.NET SDK** - PDF rendering engine
- **xUnit** - Testing framework

## 🚀 Getting Started

### Prerequisites

- Visual Studio 2022 or later
- .NET 6.0 SDK or later
- Windows 10/11

### Build and Run

1. **Open the solution**:
   ```
   Open PdfViewer.sln in Visual Studio
   ```

2. **Restore NuGet packages**:
   ```
   Right-click solution → Restore NuGet Packages
   ```

3. **Build the solution**:
   ```
   Build → Build Solution (Ctrl+Shift+B)
   ```

4. **Run the application**:
   ```
   Debug → Start Debugging (F5)
   ```

### Running Tests

```
Test → Run All Tests (Ctrl+R, A)
```

## ✨ Features

### Currently Implemented
- Basic WPF application structure
- Clean architecture setup
- Core interfaces and models
- Unit test framework

### Planned Features
- PDF file opening and rendering
- Zoom and rotation controls
- Page navigation
- Thumbnail sidebar
- Search functionality
- Annotation tools
- Print support
- Export capabilities

## 📚 Documentation

Detailed documentation is available in the `docs/` folder:

- **[PROJECT_SUMMARY.md](docs/PROJECT_SUMMARY.md)** - Complete project architecture and goals
- **[SESSION_LOG.md](docs/SESSION_LOG.md)** - Development history and decisions
- **[TESTING_GUIDE.md](docs/TESTING_GUIDE.md)** - How to write and run tests

## 🎯 Development Status

**Current Phase**: Infrastructure Setup
- ✅ Project structure created
- ✅ Solution and projects configured
- ✅ Clean architecture layers defined
- 🔄 Core rendering implementation (in progress)
- ⏳ UI implementation (planned)
- ⏳ Advanced features (planned)

## 🤝 Contributing

This is a personal project. For questions or suggestions, please refer to the documentation in the `docs/` folder.

## 📄 License

Copyright © 2026. All rights reserved.
