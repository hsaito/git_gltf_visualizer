using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;

namespace GitGltfVisualizer;

public static class GltfGenerator
{
    private const float LaneSpacing = 3.0f;
    private const float CommitSpacing = 2.0f;
    private const float NodeHalfSize = 0.4f;
    private const float TagHalfSize = 0.6f;
    private const float EdgeHalfWidth = 0.05f;

    public static void Generate(
        List<CommitInfo> commits,
        List<BranchInfo> branches,
        List<TagInfo> tags,
        string repoPath,
        string outputPath)
    {
        // Sort chronologically (oldest first) for layout
        commits = commits.OrderBy(c => c.Date).ThenBy(c => c.Hash).ToList();
        var commitMap = commits.ToDictionary(c => c.Hash);
        var branchHeadSet = new HashSet<string>(branches.Select(b => b.CommitHash));
        var tagSet = new HashSet<string>(tags.Select(t => t.CommitHash));

        // Reverse lookup: commit hash → branch/tag names
        var commitBranches = BuildLookup(branches, b => b.CommitHash, b => b.Name);
        var commitTags = BuildLookup(tags, t => t.CommitHash, t => t.Name);

        // Assign each commit to a display lane (X position)
        var lanes = AssignLanes(commits, branches, commitMap);

        // Compute 3D position for each commit
        var positions = new Dictionary<string, Vector3>(commits.Count);
        for (int i = 0; i < commits.Count; i++)
        {
            int lane = lanes.GetValueOrDefault(commits[i].Hash, 0);
            positions[commits[i].Hash] = new Vector3(lane * LaneSpacing, 0, i * CommitSpacing);
        }

        // --- Materials ---
        var matCommit = MakeMaterial("Commit", 0.3f, 0.5f, 0.9f);
        var matTag = MakeMaterial("Tag", 0.95f, 0.75f, 0.1f);
        var matBranch = MakeMaterial("BranchHead", 0.2f, 0.85f, 0.3f);
        var matMerge = MakeMaterial("Merge", 0.8f, 0.3f, 0.8f);
        var matEdge = MakeMaterial("Edge", 0.5f, 0.5f, 0.5f);

        // --- Mesh builders ---
        var commitMeshB = BuildBox("CommitBox", NodeHalfSize, matCommit);
        var tagMeshB = BuildBox("TagBox", TagHalfSize, matTag);
        var branchMeshB = BuildBox("BranchBox", NodeHalfSize * 1.3f, matBranch);
        var mergeMeshB = BuildBox("MergeBox", NodeHalfSize * 1.1f, matMerge);

        // Edge mesh: one mesh containing all parent→child connections
        var edgeMeshB = new MeshBuilder<VertexPosition>("Edges");
        bool hasEdges = false;
        foreach (var c in commits)
        {
            foreach (var ph in c.ParentHashes)
            {
                if (positions.TryGetValue(ph, out var parentPos))
                {
                    AddTube(edgeMeshB, matEdge, positions[c.Hash], parentPos, EdgeHalfWidth);
                    hasEdges = true;
                }
            }
        }

        // --- Build Schema2 model ---
        var model = ModelRoot.CreateModel();
        model.Asset.Generator = "GitGltfVisualizer";
        var scene = model.UseScene("GitHistory");

        // Convert mesh builders → Schema2 meshes
        var builders = new List<IMeshBuilder<MaterialBuilder>>
            { commitMeshB, tagMeshB, branchMeshB, mergeMeshB };
        if (hasEdges) builders.Add(edgeMeshB);
        var meshes = model.CreateMeshes(builders.ToArray());

        // Static edge node (always visible)
        if (hasEdges)
        {
            scene.CreateNode("Edges").Mesh = meshes[4];
        }

        // --- Animation timing ---
        float timePerCommit = Math.Clamp(30f / Math.Max(commits.Count, 1), 0.05f, 0.5f);
        float totalDuration = commits.Count * timePerCommit + 0.5f;

        // --- Commit nodes ---
        for (int i = 0; i < commits.Count; i++)
        {
            var c = commits[i];
            bool isTag = tagSet.Contains(c.Hash);
            bool isBranch = branchHeadSet.Contains(c.Hash);
            bool isMerge = c.ParentHashes.Count > 1;

            int meshIdx = isTag ? 1 : isBranch ? 2 : isMerge ? 3 : 0;
            string nodeType = isTag ? "tag" : isBranch ? "branchHead" : isMerge ? "merge" : "commit";

            var node = scene.CreateNode($"commit_{c.ShortHash}");
            node.Mesh = meshes[meshIdx];
            node.LocalTransform = Matrix4x4.CreateTranslation(positions[c.Hash]);

            // Metadata in glTF extras
            var extras = new Dictionary<string, object>
            {
                ["hash"] = c.Hash,
                ["shortHash"] = c.ShortHash,
                ["author"] = c.Author,
                ["authorEmail"] = c.AuthorEmail,
                ["date"] = c.Date.ToString("o"),
                ["message"] = c.Message,
                ["parents"] = c.ParentHashes,
                ["type"] = nodeType
            };
            if (commitBranches.TryGetValue(c.Hash, out var bn)) extras["branches"] = bn;
            if (commitTags.TryGetValue(c.Hash, out var tn)) extras["tags"] = tn;
            node.Extras = JsonNode.Parse(JsonSerializer.Serialize(extras));

            // Scale animation: pop-in effect
            float t = i * timePerCommit;
            if (t < 0.001f)
            {
                // First commit — always visible
                node.WithScaleAnimation("CommitProgression",
                    (0f, Vector3.One),
                    (totalDuration, Vector3.One));
            }
            else
            {
                // Hidden until appear time, then scale up over 0.15 s
                node.WithScaleAnimation("CommitProgression",
                    (0f, Vector3.Zero),
                    (t, Vector3.Zero),
                    (t + 0.15f, Vector3.One),
                    (totalDuration, Vector3.One));
            }
        }

        // Scene-level metadata
        scene.Extras = JsonNode.Parse(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["generator"] = "GitGltfVisualizer",
            ["repository"] = Path.GetFileName(repoPath.TrimEnd(Path.DirectorySeparatorChar)),
            ["commitCount"] = commits.Count,
            ["branchCount"] = branches.Count,
            ["tagCount"] = tags.Count,
            ["animationDuration"] = totalDuration,
            ["branches"] = branches.Select(b => new Dictionary<string, object>
            {
                ["name"] = b.Name,
                ["commit"] = b.CommitHash,
                ["isRemote"] = b.IsRemote
            }).ToList(),
            ["tags"] = tags.Select(t => new Dictionary<string, object>
            {
                ["name"] = t.Name,
                ["commit"] = t.CommitHash
            }).ToList()
        }));

        // Save both formats
        model.SaveGLTF(outputPath);
        model.SaveGLB(Path.ChangeExtension(outputPath, ".glb"));
    }

    // ─── Lane assignment ──────────────────────────────────────────────────

    private static Dictionary<string, int> AssignLanes(
        List<CommitInfo> commits,
        List<BranchInfo> branches,
        Dictionary<string, CommitInfo> commitMap)
    {
        var lanes = new Dictionary<string, int>();
        int nextLane = 0;

        // Process local branches; main/master gets lane 0
        var ordered = branches
            .Where(b => !b.IsRemote)
            .OrderByDescending(b => b.Name is "main" or "master")
            .ToList();

        foreach (var branch in ordered)
        {
            int lane = nextLane++;
            string? h = branch.CommitHash;
            while (h != null && commitMap.TryGetValue(h, out var ci) && !lanes.ContainsKey(h))
            {
                lanes[h] = lane;
                h = ci.ParentHashes.Count > 0 ? ci.ParentHashes[0] : null;
            }
        }

        // Remaining commits (remote-only or unreachable) go to one extra lane
        foreach (var c in commits)
            lanes.TryAdd(c.Hash, nextLane);

        return lanes;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static Dictionary<string, List<string>> BuildLookup<T>(
        IEnumerable<T> items, Func<T, string> keySelector, Func<T, string> valueSelector)
    {
        var dict = new Dictionary<string, List<string>>();
        foreach (var item in items)
        {
            string key = keySelector(item);
            if (!dict.TryGetValue(key, out var list))
                dict[key] = list = new List<string>();
            list.Add(valueSelector(item));
        }
        return dict;
    }

    private static MaterialBuilder MakeMaterial(string name, float r, float g, float b)
    {
        return new MaterialBuilder(name)
            .WithDoubleSide(true)
            .WithMetallicRoughnessShader()
            .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(r, g, b, 1));
    }

    private static MeshBuilder<VertexPosition> BuildBox(string name, float half, MaterialBuilder mat)
    {
        var mesh = new MeshBuilder<VertexPosition>(name);
        var p = mesh.UsePrimitive(mat);
        float s = half;

        p.AddQuadrangle(V(-s, -s, s), V(s, -s, s), V(s, s, s), V(-s, s, s));     // +Z
        p.AddQuadrangle(V(s, -s, -s), V(-s, -s, -s), V(-s, s, -s), V(s, s, -s)); // -Z
        p.AddQuadrangle(V(-s, s, s), V(s, s, s), V(s, s, -s), V(-s, s, -s));      // +Y
        p.AddQuadrangle(V(-s, -s, -s), V(s, -s, -s), V(s, -s, s), V(-s, -s, s)); // -Y
        p.AddQuadrangle(V(s, -s, s), V(s, -s, -s), V(s, s, -s), V(s, s, s));     // +X
        p.AddQuadrangle(V(-s, -s, -s), V(-s, -s, s), V(-s, s, s), V(-s, s, -s)); // -X

        return mesh;
    }

    private static void AddTube(
        MeshBuilder<VertexPosition> mesh, MaterialBuilder mat,
        Vector3 from, Vector3 to, float hw)
    {
        var dir = to - from;
        if (dir.LengthSquared() < 0.0001f) return;
        dir = Vector3.Normalize(dir);

        var up = Math.Abs(Vector3.Dot(dir, Vector3.UnitY)) < 0.99f
            ? Vector3.UnitY
            : Vector3.UnitX;
        var right = Vector3.Normalize(Vector3.Cross(dir, up)) * hw;
        up = Vector3.Normalize(Vector3.Cross(right, dir)) * hw;

        var a0 = from - right - up;
        var a1 = from + right - up;
        var a2 = from + right + up;
        var a3 = from - right + up;
        var b0 = to - right - up;
        var b1 = to + right - up;
        var b2 = to + right + up;
        var b3 = to - right + up;

        var p = mesh.UsePrimitive(mat);
        p.AddQuadrangle(V(a0), V(a1), V(b1), V(b0));
        p.AddQuadrangle(V(a1), V(a2), V(b2), V(b1));
        p.AddQuadrangle(V(a2), V(a3), V(b3), V(b2));
        p.AddQuadrangle(V(a3), V(a0), V(b0), V(b3));
    }

    private static VertexPosition V(float x, float y, float z) => new(new Vector3(x, y, z));
    private static VertexPosition V(Vector3 v) => new(v);
}
