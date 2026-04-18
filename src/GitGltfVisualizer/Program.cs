namespace GitGltfVisualizer;

class Program
{
    static int Main(string[] args)
    {
        string repoPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

        if (!Directory.Exists(Path.Combine(repoPath, ".git")) &&
            !File.Exists(Path.Combine(repoPath, ".git")))
        {
            Console.Error.WriteLine($"Error: '{repoPath}' is not a git repository.");
            return 1;
        }

        try
        {
            Console.WriteLine($"Scanning repository: {repoPath}");

            var commits = GitReader.GetCommits(repoPath);
            var branches = GitReader.GetBranches(repoPath);
            var tags = GitReader.GetTags(repoPath);

            if (commits.Count == 0)
            {
                Console.Error.WriteLine("No commits found.");
                return 1;
            }

            Console.WriteLine($"  {commits.Count} commits, {branches.Count} branches, {tags.Count} tags");

            string outputPath = Path.Combine(repoPath, "git_history.gltf");
            GltfGenerator.Generate(commits, branches, tags, repoPath, outputPath);

            Console.WriteLine($"Output: {outputPath}");
            Console.WriteLine($"Output: {Path.ChangeExtension(outputPath, ".glb")}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
