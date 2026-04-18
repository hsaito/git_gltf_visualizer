using System.Text.Json;

namespace GitGltfVisualizer.Tests;

public class GltfGeneratorTests
{
    [Fact]
    public void Generate_WithoutAnimation_ProducesStaticModel()
    {
        using var temp = new TempDirectory();
        string outputPath = Path.Combine(temp.Path, "history.gltf");

        var commits = new List<CommitInfo>
        {
            new(
                Hash: "1111111111111111111111111111111111111111",
                ShortHash: "1111111",
                Author: "Test Author",
                AuthorEmail: "test@example.com",
                Date: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Message: "initial commit",
                ParentHashes: new List<string>())
        };

        var branches = new List<BranchInfo>
        {
            new("main", commits[0].Hash, false)
        };

        var tags = new List<TagInfo>
        {
            new("v1.0.0", commits[0].Hash)
        };

        GltfGenerator.Generate(commits, branches, tags, temp.Path, outputPath, animate: false);

        Assert.True(File.Exists(outputPath));
        Assert.True(File.Exists(Path.ChangeExtension(outputPath, ".glb")));

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.False(doc.RootElement.TryGetProperty("animations", out _));
    }

    [Fact]
    public void Generate_WithAnimation_WritesAnimationsAndSeparateTagNode()
    {
        using var temp = new TempDirectory();
        string outputPath = Path.Combine(temp.Path, "history.gltf");

        var commits = new List<CommitInfo>
        {
            new(
                Hash: "1111111111111111111111111111111111111111",
                ShortHash: "1111111",
                Author: "Test Author",
                AuthorEmail: "test@example.com",
                Date: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Message: "initial commit",
                ParentHashes: new List<string>()),
            new(
                Hash: "2222222222222222222222222222222222222222",
                ShortHash: "2222222",
                Author: "Test Author",
                AuthorEmail: "test@example.com",
                Date: new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero),
                Message: "second commit",
                ParentHashes: new List<string> { "1111111111111111111111111111111111111111" })
        };

        var branches = new List<BranchInfo>
        {
            new("main", commits[1].Hash, false)
        };

        var tags = new List<TagInfo>
        {
            new("v1.0.0", commits[1].Hash)
        };

        GltfGenerator.Generate(commits, branches, tags, temp.Path, outputPath, animate: true);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.True(doc.RootElement.TryGetProperty("animations", out var animations));
        Assert.True(animations.GetArrayLength() > 0);

        Assert.True(doc.RootElement.TryGetProperty("nodes", out var nodes));
        var nodeNames = nodes.EnumerateArray()
            .Where(n => n.TryGetProperty("name", out _))
            .Select(n => n.GetProperty("name").GetString())
            .Where(n => n is not null)
            .ToList();

        Assert.Contains("commit_2222222", nodeNames);
        Assert.Contains("tag_2222222", nodeNames);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GitGltfVisualizerTests",
            Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
