# ObsidianToPdf

A C# tool to convert an Obsidian Markdown vault (or folder) into a single PDF file.

## Features
- Recursively scans a directory for Markdown (`.md`) files.
- Resolves Obsidian WikiLinks `[[Note]]` to internal PDF anchors.
- Embeds images `![[Image.png]]` (supports local files).
- Generates a Table of Contents.
- CSS styling for print layout.

## Prerequisites
- .NET SDK (8.0 or later recommended).
- Internet connection (first run only) to download the Chromium browser for PDF rendering.

## Usage

1. **Build the project:**
   ```bash
   cd ObsidianToPdf
   dotnet build
   ```

2. **Run the tool:**
   ```bash
   dotnet run -- "<InputFolderPath>" "<OutputPdfPath>"
   ```

   **Example:**
   ```bash
   dotnet run -- "/path/to/my/obsidian/vault" "/path/to/output.pdf"
   ```

## Notes
- The tool uses `PuppeteerSharp` which downloads a local version of Chromium.
- Images must be within the scanned directory or subdirectories to be correctly resolved.
- Large vaults may take some time to process.
