using System;
using SourceGit.ViewModels;

namespace SourceGit.Models
{
    public enum ChangeViewMode
    {
        List,
        Grid,
        Tree,
    }

    public enum ChangeState
    {
        None,
        Modified,
        TypeChanged,
        Added,
        Deleted,
        Renamed,
        Copied,
        Unmerged,
        Untracked
    }

    public class ChangeDataForAmend
    {
        public string FileMode { get; set; } = "";
        public string ObjectHash { get; set; } = "";
    }

    public class Change
    {
        public Change() { }
        public Change(Change c)
        {
            Repo = c.Repo;
            Index = c.Index;
            WorkTree = c.WorkTree;
            Path = c.Path;
            OriginalPath = c.OriginalPath;
            DataForAmend = c.DataForAmend;
        }

        public Repository Repo { get; set; }
        public ChangeState Index { get; set; } = ChangeState.None;
        public ChangeState WorkTree { get; set; } = ChangeState.None;
        public string Path { get; set; }

        private string _groupPath;
        public string GroupPath
        {
            get
            {
                if (Repo == null)
                {
                    return Path;
                }
                else
                {
                    if (_groupPath == null)
                    {
                        if (App.GetLauncer().ActivePage.Data is RepositoryGroup group)
                        {
                            _groupPath = $"{Repo.FullPath.Substring(group.PathPrefix.Length)}/{Path}".Trim('/');
                        }
                        else
                        {
                            _groupPath = $"{Repo.FullPath}/{Path}".Trim('/');
                        }
                    }

                    return _groupPath;
                }
            }
        }

        public string OriginalPath { get; set; } = "";
        public ChangeDataForAmend DataForAmend { get; set; } = null;

        public bool IsConflit
        {
            get
            {
                if (Index == ChangeState.Unmerged || WorkTree == ChangeState.Unmerged)
                    return true;
                if (Index == ChangeState.Added && WorkTree == ChangeState.Added)
                    return true;
                if (Index == ChangeState.Deleted && WorkTree == ChangeState.Deleted)
                    return true;
                return false;
            }
        }

        public void Set(ChangeState index, ChangeState workTree = ChangeState.None)
        {
            Index = index;
            WorkTree = workTree;

            if (index == ChangeState.Renamed || workTree == ChangeState.Renamed)
            {
                var idx = Path.IndexOf('\t', StringComparison.Ordinal);
                if (idx >= 0)
                {
                    OriginalPath = Path.Substring(0, idx);
                    Path = Path.Substring(idx + 1);
                }
                else
                {
                    idx = Path.IndexOf(" -> ", StringComparison.Ordinal);
                    if (idx > 0)
                    {
                        OriginalPath = Path.Substring(0, idx);
                        Path = Path.Substring(idx + 4);
                    }
                }
            }

            if (Path[0] == '"')
                Path = Path.Substring(1, Path.Length - 2);
            if (!string.IsNullOrEmpty(OriginalPath) && OriginalPath[0] == '"')
                OriginalPath = OriginalPath.Substring(1, OriginalPath.Length - 2);
        }
    }
}
