# Obsidian to Blog Converter

This is a C# command-line tool to convert Obsidian markdown files into formats suitable for various blogging platforms (Zenn, Qiita, Note) and to help import them.

## Features

-   Converts a single markdown file or a directory of files.
-   Applies platform-specific frontmatter based on a template.
-   For directories, generates a table of contents for Qiita and Note.
-   For directories, generates a Zenn-compatible book structure (`book.yaml` and article slugs).
-   Provides an importer for Qiita (via API).

## Installation and Building

You need the .NET 8 SDK installed.

To build the project, run:
```bash
dotnet build
```

## Usage

The tool has two main commands: `convert` and `import`.

### 1. `convert`

This command converts your local Obsidian markdown files into formats ready for each platform.

**Syntax:**
```bash
dotnet run --project ObsidianToBlog/ObsidianToBlog.csproj -- convert <path> [--output <dir>]
```

-   `<path>`: The path to a single `.md` file or a directory containing `.md` files.
-   `--output <dir>`: (Optional) The directory where the converted files will be saved. Defaults to `./output`.

**Example (Single File):**
```bash
dotnet run --project ObsidianToBlog/ObsidianToBlog.csproj -- convert my-article.md
```
This will create the following structure:
```
./output/
├── zenn/
│   └── articles/
│       └── [random_slug].md
├── qiita/
│   └── My_Article.md
└── note/
    └── My_Article.md
```

**Example (Directory / Series):**
```bash
dotnet run --project ObsidianToBlog/ObsidianToBlog.csproj -- convert ./my-series --output ./my-zenn-repo
```
This will process all `.md` files in `./my-series`, and if you specify the output as a Zenn repository, it will generate the correct structure:
```
./my-zenn-repo/
├── zenn/
│   ├── articles/
│   │   ├── [slug_for_part1].md
│   │   └── [slug_for_part2].md
│   └── book.yaml  <-- Generated for the series
├── qiita/
│   ├── Part_1.md  <-- Contains a TOC
│   └── Part_2.md  <-- Contains a TOC
└── note/
    ├── Part_1.md  <-- Contains a TOC
    └── Part_2.md  <-- Contains a TOC
```

### 2. `import`

This command imports a **previously converted** file to a specific platform.

#### Qiita

This command uploads an article to Qiita using the v2 API.

**Prerequisites:**
You must set your Qiita API access token as an environment variable:
```bash
export QIITA_API_TOKEN="your_qiita_api_token"
```

**Syntax:**
```bash
dotnet run --project ObsidianToBlog/ObsidianToBlog.csproj -- import qiita <path_to_qiita_file>
```

**Example:**
```bash
dotnet run --project ObsidianToBlog/ObsidianToBlog.csproj -- import qiita ./output/qiita/My_Article.md
```
The tool will post the article to your Qiita account as a private post.

---

## Platform-Specific Workflows

### Zenn

1.  Use the `convert` command with the `--output` flag pointing to your local Zenn content repository.
    ```bash
    dotnet run --project ObsidianToBlog/ObsidianToBlog.csproj -- convert ./my-book-directory --output ./my-zenn-repo
    ```
2.  The tool will generate the `articles` and the `book.yaml` file in the correct location (`./my-zenn-repo/zenn/`).
3.  Navigate to your Zenn repository and use the Zenn CLI to preview your posts.
    ```bash
    cd ./my-zenn-repo/zenn
    npx zenn preview
    ```

### Qiita

1.  First, use the `convert` command to generate the Qiita-formatted markdown file.
2.  Set your `QIITA_API_TOKEN` environment variable.
3.  Use the `import qiita` command to upload the file.

### Note

1.  Use the `convert` command to generate the Note-formatted markdown file.
2.  Note's official import tool uses WXR/MT formats, which this tool does not generate.
3.  The recommended workflow is to **manually copy and paste** the content from the generated `.md` file into the Note editor.
