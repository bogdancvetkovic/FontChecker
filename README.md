# FontChecker

A console tool that scans PDF files and reports which fonts are used and whether they are embedded.

## Why

Print shops need PDFs with embedded fonts. A missing font can change the layout or break the print job, and checking files one by one in a PDF viewer is slow. FontChecker checks a single file or a whole folder at once and shows exactly which pages have a problem.

## Features

- Scans one PDF or an entire folder (including subfolders)
- Shows each font once, with type, embedded status and subset status
- Lists the exact pages where a font is not embedded
- Summary per file and per folder
- Handles Type0, TrueType and Type3 fonts
- Single `.exe`, no installation needed

## Usage

Drag a PDF or a folder onto `FontChecker.exe`, or run it from a terminal:

```
FontChecker.exe "C:\path\to\file-or-folder"
```

## Build from source

Requires the .NET 10 SDK.

```
dotnet run -- "C:\path\to\file-or-folder"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Built with

- C# / .NET 10
- [PdfPig](https://github.com/UglyToad/PdfPig) for reading the PDF structure
- [Spectre.Console](https://spectreconsole.net/) for the terminal interface

## Roadmap

- CSV export
- Desktop window (GUI)