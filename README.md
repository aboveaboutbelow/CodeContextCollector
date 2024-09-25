# CodeContextCollector

CodeContextCollector is a Visual Studio extension designed to streamline the process of gathering comprehensive code context for use with coding assistants.

## Features

- **Copy Open Files Content**: Copies the content of all open files to the clipboard, formatted with file names and code blocks.
- **Open Referenced Types**: Analyzes the current C# file and opens files containing referenced types, making it easier to explore related code.

## Installation

1. Download the VSIX file from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/).
2. Double-click the downloaded file to install the extension.
3. Restart Visual Studio to complete the installation.

## Usage

After installation, you'll find two new commands in the Tools menu:

1. **Copy Open Files Content to Clipboard**: Click to copy all open file contents.
2. **Open Referenced Types**: With a C# file open, click to analyze and open files with referenced types.

## Requirements

- Visual Studio 2022 (Version 17.0 or later)
- .NET Framework 4.5 or later
