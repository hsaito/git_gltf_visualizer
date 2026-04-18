using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace GitGltfVisualizer;

public record CommitInfo(
    string Hash,
    string ShortHash,
    string Author,
    string AuthorEmail,
    DateTimeOffset Date,
    string Message,
    List<string> ParentHashes);

public record BranchInfo(string Name, string CommitHash, bool IsRemote);

public record TagInfo(string Name, string CommitHash);

public static class GitReader
{
    private static string RunGit(string arguments, string workingDirectory)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {arguments} failed (exit {process.ExitCode}): {error}");
        return output;
    }

    public static List<CommitInfo> GetCommits(string repoPath)
    {
        // Use git's %x1f hex escape for unit separator — safe against special chars in fields
        string output = RunGit(
            "log --all --format=\"%H%x1f%h%x1f%an%x1f%ae%x1f%aI%x1f%P%x1f%s\"",
            repoPath);

        var commits = new List<CommitInfo>();
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('\x1f', 7);
            if (parts.Length < 7) continue;

            var parentHashes = string.IsNullOrWhiteSpace(parts[5])
                ? new List<string>()
                : parts[5].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

            if (DateTimeOffset.TryParse(parts[4].Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                commits.Add(new CommitInfo(
                    parts[0].Trim(), parts[1].Trim(),
                    parts[2].Trim(), parts[3].Trim(),
                    date, parts[6].Trim(), parentHashes));
            }
        }
        return commits;
    }

    public static List<BranchInfo> GetBranches(string repoPath)
    {
        string output = RunGit(
            "for-each-ref --format=\"%(refname:short) %(objectname)\" refs/heads refs/remotes",
            repoPath);

        var branches = new List<BranchInfo>();
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            int lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace < 0) continue;
            string name = trimmed[..lastSpace];
            string hash = trimmed[(lastSpace + 1)..];
            if (string.IsNullOrEmpty(name) || hash.Length < 40) continue;
            bool isRemote = name.StartsWith("origin/");
            branches.Add(new BranchInfo(name, hash, isRemote));
        }
        return branches;
    }

    public static List<TagInfo> GetTags(string repoPath)
    {
        // %(*objectname) dereferences annotated tags to the commit; empty for lightweight.
        // Concatenating %(*objectname)%(objectname) gives commit-hash first for annotated,
        // or just commit-hash for lightweight. Take first 40 chars → always the commit.
        string output = RunGit(
            "for-each-ref --format=\"%(refname:short) %(*objectname)%(objectname)\" refs/tags",
            repoPath);

        var tags = new List<TagInfo>();
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            int lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace < 0) continue;
            string name = trimmed[..lastSpace];
            string hash = trimmed[(lastSpace + 1)..];
            if (string.IsNullOrEmpty(name) || hash.Length < 40) continue;
            if (hash.Length > 40) hash = hash[..40];
            tags.Add(new TagInfo(name, hash));
        }
        return tags;
    }
}
