using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using SourceGit.Models;
using SourceGit.Views;

namespace SourceGit.ViewModels
{
    public class RepositoryGroup : ObservableObject
    {
        public AvaloniaList<RepositoryNode> Rows
        {
            get;
            private set;
        } = [];

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    Refresh();
            }
        }

        public string PathPrefix => _pathPrefix;
        public object Data
        {
            get => _repo;
            set => SetProperty(ref _repo, value);
        }

        public List<Repository> Repositories { get; set; } = new List<Repository>();

        public RepositoryNode Group { get; private set; }

        public RepositoryGroup(RepositoryNode group)
        {
            Group = group;
            Refresh();
        }


        public void Refresh()
        {
            if (string.IsNullOrWhiteSpace(_searchFilter))
            {
                    ResetVisibility(Group);
            }
            else
            {
                    SetVisibilityBySearch(Group);
            }

            var rows = new List<RepositoryNode>();
            MakeTreeRows(rows, new List<RepositoryNode>() { Group });
            Rows.Clear();
            Rows.AddRange(rows);

            foreach (var row in Rows)
            {
                if (row.IsRepository)
                {
                    OpenRepo(row);
                }
            }

            _pathPrefix = Repositories[0].FullPath;
            foreach (var repo in Repositories)
            {
                _pathPrefix = LongestCommonPrefix(_pathPrefix, repo.FullPath);
            }
        }

        public void ToggleNodeIsExpanded(RepositoryNode node)
        {
            node.IsExpanded = !node.IsExpanded;

            var depth = node.Depth;
            var idx = Rows.IndexOf(node);
            if (idx == -1)
                return;

            if (node.IsExpanded)
            {
                var subrows = new List<RepositoryNode>();
                MakeTreeRows(subrows, node.SubNodes, depth + 1);
                Rows.InsertRange(idx + 1, subrows);
            }
            else
            {
                var removeCount = 0;
                for (int i = idx + 1; i < Rows.Count; i++)
                {
                    var row = Rows[i];
                    if (row.Depth <= depth)
                        break;

                    removeCount++;
                }
                Rows.RemoveRange(idx + 1, removeCount);
            }
        }

        public void InitRepository(string path, RepositoryNode parent, string reason)
        {
            if (!Preference.Instance.IsGitConfigured())
            {
                App.RaiseException(PopupHost.Active.GetId(), App.Text("NotConfigured"));
                return;
            }

            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new Init(path, parent, reason));
        }

        public void Clone()
        {
            if (!Preference.Instance.IsGitConfigured())
            {
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
                return;
            }

            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new Clone());
        }

        public void OpenTerminal()
        {
            if (!Preference.Instance.IsGitConfigured())
                App.RaiseException(PopupHost.Active.GetId(), App.Text("NotConfigured"));
            else
                Native.OS.OpenTerminal(null);
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public void AddRootNode()
        {
            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new CreateGroup(Group));
        }
        public void MoveNode(RepositoryNode from, RepositoryNode to)
        {
            Preference.Instance.MoveNode(from, to, true);
            Refresh();
        }

        public ContextMenu CreateContextMenu(RepositoryNode node)
        {
            var menu = new ContextMenu();

            if (node.IsRepository)
            {
                var open = new MenuItem();
                open.Header = App.Text("Welcome.OpenOrInit");
                open.Icon = App.CreateMenuIcon("Icons.Folder.Open");
                open.Click += (_, e) =>
                {
                    var repo = Repositories.Where(r => r.RepositoryNode.Id == node.Id).FirstOrDefault();
                    
                    App.GetLauncer()?.OpenRepositoryInTab(node, new LauncherPage(node, repo));
                    e.Handled = true;
                };

                var explore = new MenuItem();
                explore.Header = App.Text("Repository.Explore");
                explore.Icon = App.CreateMenuIcon("Icons.Explore");
                explore.Click += (_, e) =>
                {
                    node.OpenInFileManager();
                    e.Handled = true;
                };

                var terminal = new MenuItem();
                terminal.Header = App.Text("Repository.Terminal");
                terminal.Icon = App.CreateMenuIcon("Icons.Terminal");
                terminal.Click += (_, e) =>
                {
                    node.OpenTerminal();
                    e.Handled = true;
                };

                menu.Items.Add(open);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(explore);
                menu.Items.Add(terminal);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }
            else
            {
                var addSubFolder = new MenuItem();
                addSubFolder.Header = App.Text("Welcome.AddSubFolder");
                addSubFolder.Icon = App.CreateMenuIcon("Icons.Folder.Add");
                addSubFolder.Click += (_, e) =>
                {
                    node.AddSubFolder();
                    e.Handled = true;
                };
                menu.Items.Add(addSubFolder);
            }

            var edit = new MenuItem();
            edit.Header = App.Text("Welcome.Edit");
            edit.Icon = App.CreateMenuIcon("Icons.Edit");
            edit.Click += (_, e) =>
            {
                node.Edit();
                e.Handled = true;
            };

            var move = new MenuItem();
            move.Header = App.Text("Welcome.Move");
            move.Icon = App.CreateMenuIcon("Icons.MoveToAnotherGroup");
            move.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new MoveRepositoryNode(node));

                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = App.Text("Welcome.Delete");
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                node.Delete();
                e.Handled = true;
            };

            menu.Items.Add(edit);
            menu.Items.Add(move);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(delete);

            return menu;
        }

        private void ResetVisibility(RepositoryNode node)
        {
            node.IsVisible = true;
            foreach (var subNode in node.SubNodes)
                ResetVisibility(subNode);
        }

        private void SetVisibilityBySearch(RepositoryNode node)
        {
            if (!node.IsRepository)
            {
                if (node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    node.IsVisible = true;
                    foreach (var subNode in node.SubNodes)
                        ResetVisibility(subNode);
                }
                else
                {
                    bool hasVisibleSubNode = false;
                    foreach (var subNode in node.SubNodes)
                    {
                        SetVisibilityBySearch(subNode);
                        hasVisibleSubNode |= subNode.IsVisible;
                    }
                    node.IsVisible = hasVisibleSubNode;
                }
            }
            else
            {
                node.IsVisible = node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    node.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void MakeTreeRows(List<RepositoryNode> rows, List<RepositoryNode> nodes, int depth = 0)
        {
            foreach (var node in nodes)
            {
                if (!node.IsVisible)
                    continue;

                node.Depth = depth;
                node.Context = this;
                rows.Add(node);

                if (node.IsRepository || !node.IsExpanded)
                    continue;

                MakeTreeRows(rows, node.SubNodes, depth + 1);
            }
        }

        private void OpenGroup(Launcher launcher, RepositoryNode node)
        {
            launcher.OpenGroupInTab(node);
        }

        internal void OpenRepo(RepositoryNode node)
        {
            if (!node.IsRepository)
            {
                return;
            }

            var repo = Repositories.Where(r => r.RepositoryNode.Id == node.Id).FirstOrDefault();
            if (repo != null)
            {
                Data = repo;
            }
            else
            {
                var gitDir = new Commands.QueryGitDir(node.Id).Result();
                if (string.IsNullOrEmpty(gitDir))
                {
                    App.RaiseException(node.Id, "Given path is not a valid git repository!");
                    return;
                }

                repo = new Repository()
                {
                    FullPath = node.Id,
                    GitDir = gitDir,
                    RepositoryNode = node,
                };

                repo.Open();

                Data = repo;
                Repositories.Add(repo);

                if (App.GetLauncer() != null && !App.GetLauncer().ActiveWorkspace.Groups.Contains(Group.Id))
                {
                    App.GetLauncer().ActiveWorkspace.Groups.Add(Group.Id);
                }
            }
        }

        internal async void Fetch()
        {
            foreach (var repo in Repositories)
            {
                var fetch = new Fetch(repo);
                PopupHost.ShowPopup(fetch);
                repo.SetBusy(true);
                fetch.InProgress = true;
                await fetch.Sure();
                fetch.InProgress = false;
                repo.SetBusy(false);
            }

            PopupHost.Active.Popup = null;
        }

        internal async void Pull()
        {
            foreach (var repo in Repositories)
            {
                var pull = new Pull(repo, null);
                pull.PreAction = Models.DealWithLocalChanges.StashAndReaply;
                pull.UseRebase = false;
                PopupHost.ShowPopup(pull);
                repo.SetBusy(true);
                pull.InProgress = true;
                await pull.Sure();
                pull.InProgress = false;
                repo.SetBusy(false);
            }

            PopupHost.Active.Popup = null;
        }

        internal async void Push()
        {
            foreach (var repo in Repositories)
            {
                if (repo.CurrentBranch.TrackStatus.Ahead.Count > 0)
                {
                    var push = new Push(repo, null);
                    PopupHost.ShowPopup(push);
                    repo.SetBusy(true);
                    push.InProgress = true;
                    await push.Sure();
                    push.InProgress = false;
                    repo.SetBusy(false);
                }
            }

            PopupHost.Active.Popup = null;
        }

        public static string LongestCommonPrefix(string str1, string str2)
        {
            if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            {
                return string.Empty;
            }

            int minLength = Math.Min(str1.Length, str2.Length);
            int i = 0;

            while (i < minLength && str1[i] == str2[i])
            {
                i++;
            }

            return str1.Substring(0, i);
        }

        internal void Changes()
        {
            if (_workCopyGroup == null)
                _workCopyGroup = new WorkingCopyGroup(this, Repositories);

            Data = _workCopyGroup;

            MarkWorkingCopyDirtyManually();
        }

        public void MarkWorkingCopyDirtyManually()
        {
            var changes = new List<Change>();

            foreach (Repository repo in Repositories)
            {
                changes.AddRange(repo.WorkingCopy.Staged.Where(c => { c.Repo = repo; return true; }));
                changes.AddRange(repo.WorkingCopy.Unstaged.Where(c => { c.Repo = repo; return true; }));
            }

            _workCopyGroup.SetData(changes);
        }

        private static Welcome _instance = new Welcome();
        private string _searchFilter = string.Empty;
        private object _repo = null;
        private WorkingCopyGroup _workCopyGroup = null;
        private string _pathPrefix;
    }
}
