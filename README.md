# Git GLTF Visualizer

Generate a 3D glTF representation of a Git repository history.

Current version: `0.1.0`

The tool reads commits, branches, and tags from a repository and produces:

- `git_history.gltf`
- `git_history.glb`

## Features

- Visualizes commits and parent relationships as 3D nodes and edges
- Renders branch heads and merge commits with distinct styling
- Renders tags as separate nodes above commits (with connecting stems)
- Optional commit progression animation via CLI flag
- Uses `System.CommandLine` for modern CLI parsing

## Requirements

- Windows (current publish target is `win-x64`)
- .NET SDK 10.0+
- `git` available in PATH

## Build

```bash
dotnet build git_gltf_visualizer.sln
```

## Test

```bash
dotnet test git_gltf_visualizer.sln
```

## Run from source

```bash
dotnet run --project src/GitGltfVisualizer -- <repo-path>
```

Enable animation:

```bash
dotnet run --project src/GitGltfVisualizer -- <repo-path> --animation
```

Short form:

```bash
dotnet run --project src/GitGltfVisualizer -- <repo-path> -a
```

If `<repo-path>` is omitted, the current directory is used.

## Publish single-file executable

The project is configured to publish a self-contained single-file binary.

```bash
dotnet publish src/GitGltfVisualizer/GitGltfVisualizer.csproj -c Release
```

Output executable:

- `src/GitGltfVisualizer/bin/Release/net10.0/win-x64/publish/GitGltfVisualizer.exe`

## Command line usage

```text
GitGltfVisualizer [repo] [--animation|-a]
```

- `repo`: Path to a Git repository (defaults to current directory)
- `--animation`, `-a`: Enable pop-in animation for commit/tag nodes

## Output

For the target repository, generated files are written to:

- `<repo>/git_history.gltf`
- `<repo>/git_history.glb`

## Project layout

- `src/GitGltfVisualizer`: Application source
- `tests/GitGltfVisualizer.Tests`: xUnit test project
