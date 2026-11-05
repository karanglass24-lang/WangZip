using System.Collections.ObjectModel;

namespace WangZip.Core;

/// <summary>나무형 폴더 트리 노드.</summary>
public sealed class FolderNode
{
    public string Name { get; init; } = "";
    /// <summary>루트부터 "/" 구분자로 이어진 전체 경로 (루트는 빈 문자열).</summary>
    public string FullPath { get; init; } = "";
    public ObservableCollection<FolderNode> Children { get; } = [];
    public bool IsRoot { get; init; }

    public string Icon => IsRoot ? "" : "";
}

public static class FolderTreeBuilder
{
    /// <summary>항목 경로 목록으로 폴더 트리를 만듭니다. 파일 경로의 상위 폴더도 자동 생성됩니다.</summary>
    public static FolderNode Build(string rootName, IEnumerable<(string Name, bool IsDirectory)> entries)
    {
        var root = new FolderNode { IsRoot = true, Name = rootName, FullPath = "" };
        var map = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase) { [""] = root };

        static string ParentOf(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash <= 0 ? "" : path[..slash];
        }

        void Ensure(string path)
        {
            while (path.Length > 0 && !map.ContainsKey(path))
            {
                int slash = path.LastIndexOf('/');
                map[path] = new FolderNode
                {
                    Name = slash >= 0 ? path[(slash + 1)..] : path,
                    FullPath = path,
                };
                path = ParentOf(path);
            }
        }

        foreach (var (name, isDirectory) in entries)
        {
            string path = name.TrimEnd('/');
            if (path.Length == 0) continue;
            Ensure(isDirectory ? path : ParentOf(path));
        }

        foreach (var pair in map.Where(p => p.Key.Length > 0)
                     .OrderBy(p => p.Value.Name, StringComparer.CurrentCulture))
        {
            map[ParentOf(pair.Key)].Children.Add(pair.Value);
        }

        return root;
    }
}
