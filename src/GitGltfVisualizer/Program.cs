using System.CommandLine;

namespace GitGltfVisualizer;

class Program
{
    static int Main(string[] args)
    {
        var repoArg = new Argument<string>("repo")
        {
            DefaultValueFactory = _ => Directory.GetCurrentDirectory(),
            Description = "Path to the git repository (defaults to current directory)"
        };

        var animationOption = new Option<bool>("--animation", "-a")
        {
            Description = "Enable pop-in animation for commits"
        };

        var rootCommand = new RootCommand("Generate a glTF 3D visualization of a git repository's history")
        {
            repoArg,
            animationOption
        };

        rootCommand.SetAction((ParseResult result) =>
        {
            string repoPath = result.GetValue(repoArg)!;
            bool animate = result.GetValue(animationOption);

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
                Console.WriteLine($"  Animation: {(animate ? "on" : "off")}");

                string outputPath = Path.Combine(repoPath, "git_history.gltf");
                GltfGenerator.Generate(commits, branches, tags, repoPath, outputPath, animate);

                Console.WriteLine($"Output: {outputPath}");
                Console.WriteLine($"Output: {Path.ChangeExtension(outputPath, ".glb")}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return rootCommand.Parse(args, new ParserConfiguration()).Invoke(new InvocationConfiguration());
    }
}
