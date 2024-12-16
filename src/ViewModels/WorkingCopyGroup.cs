﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class WorkingCopyGroup : ObservableObject
    {

        public bool IncludeUntracked
        {
            get => _includeUntracked;
            set
            {
                if (_includeUntracked != value)
                {
                    _includeUntracked = value;

                    foreach (var repo in _repos)
                    {
                        repo.IncludeUntracked = _includeUntracked;
                    }

                    OnPropertyChanged();
                }
            }
        }

        public bool CanCommitWithPush
        {
            get => _canCommitWithPush;
            set
            {
                if (SetProperty(ref _canCommitWithPush, value))
                    OnPropertyChanged(nameof(IsCommitWithPushVisible));
            }
        }

        public bool IsStaging
        {
            get => _isStaging;
            private set => SetProperty(ref _isStaging, value);
        }

        public bool IsUnstaging
        {
            get => _isUnstaging;
            private set => SetProperty(ref _isUnstaging, value);
        }

        public bool IsCommitting
        {
            get => _isCommitting;
            private set => SetProperty(ref _isCommitting, value);
        }

        public bool AutoStageBeforeCommit
        {
            get => _autoStageBeforeCommit;
            set => _autoStageBeforeCommit = value;
        }

        public bool UseAmend
        {
            get => false;
        }

        public bool IsCommitWithPushVisible
        {
            get => !UseAmend && CanCommitWithPush;
        }

        public List<Models.Change> Unstaged
        {
            get => _unstaged;
            private set => SetProperty(ref _unstaged, value);
        }

        public List<Models.Change> Staged
        {
            get => _staged;
            private set => SetProperty(ref _staged, value);
        }

        public List<Models.Change> SelectedUnstaged
        {
            get => _selectedUnstaged;
            set
            {
                if (SetProperty(ref _selectedUnstaged, value))
                {
                    if (value == null || value.Count == 0)
                    {
                        if (_selectedStaged == null || _selectedStaged.Count == 0)
                            SetDetail(null, true);
                    }
                    else
                    {
                        if (_selectedStaged != null && _selectedStaged.Count > 0)
                            SelectedStaged = [];

                        if (value.Count == 1)
                            SetDetail(value[0], true);
                        else
                            SetDetail(null, true);
                    }
                }
            }
        }

        public List<Models.Change> SelectedStaged
        {
            get => _selectedStaged;
            set
            {
                if (SetProperty(ref _selectedStaged, value))
                {
                    if (value == null || value.Count == 0)
                    {
                        if (_selectedUnstaged == null || _selectedUnstaged.Count == 0)
                            SetDetail(null, false);
                    }
                    else
                    {
                        if (_selectedUnstaged != null && _selectedUnstaged.Count > 0)
                            SelectedUnstaged = [];

                        if (value.Count == 1)
                            SetDetail(value[0], false);
                        else
                            SetDetail(null, false);
                    }
                }
            }
        }

        public object DetailContext
        {
            get => _detailContext;
            private set => SetProperty(ref _detailContext, value);
        }

        public string CommitMessage
        {
            get => _commitMessage;
            set => SetProperty(ref _commitMessage, value);
        }

        public RepositoryGroup Group
        {
            get => _group;
        }

        public WorkingCopyGroup(RepositoryGroup group, List<Repository> repos)
        {
            _repos = repos;
            _group = group;

            foreach (var repo in _repos)
            {
                repo.WorkingCopy.PropertyChanged += WorkingCopy_PropertyChanged;
            }
        }

        private void WorkingCopy_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Staged" || e.PropertyName == "Unstaged")
            {
                _group.MarkWorkingCopyDirtyManually();
            }
        }

        public void Cleanup()
        {
            _selectedUnstaged.Clear();
            OnPropertyChanged(nameof(SelectedUnstaged));

            _selectedStaged.Clear();
            OnPropertyChanged(nameof(SelectedStaged));

            _unstaged.Clear();
            OnPropertyChanged(nameof(Unstaged));

            _staged.Clear();
            OnPropertyChanged(nameof(Staged));

            _detailContext = null;
            _commitMessage = string.Empty;

            _group = null;
            _repos = null;
        }

        public bool SetData(List<Models.Change> changes)
        {
            if (!IsChanged(_cached, changes))
            {
                // Just force refresh selected changes.
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (_selectedUnstaged.Count == 1)
                        SetDetail(_selectedUnstaged[0], true);
                    else if (_selectedStaged.Count == 1)
                        SetDetail(_selectedStaged[0], false);
                    else
                        SetDetail(null, false);
                });

                return _cached.Find(x => x.IsConflit) != null;
            }

            _cached = changes;
            _count = _cached.Count;

            var lastSelectedUnstaged = new HashSet<string>();
            var lastSelectedStaged = new HashSet<string>();
            if (_selectedUnstaged != null && _selectedUnstaged.Count > 0)
            {
                foreach (var c in _selectedUnstaged)
                    lastSelectedUnstaged.Add(c.Path);
            }
            else if (_selectedStaged != null && _selectedStaged.Count > 0)
            {
                foreach (var c in _selectedStaged)
                    lastSelectedStaged.Add(c.Path);
            }

            var unstaged = new List<Models.Change>();
            var selectedUnstaged = new List<Models.Change>();
            var hasConflict = false;
            foreach (var c in changes)
            {
                if (c.WorkTree != Models.ChangeState.None)
                {
                    unstaged.Add(c);
                    hasConflict |= c.IsConflit;

                    if (lastSelectedUnstaged.Contains(c.Path))
                        selectedUnstaged.Add(c);
                }
            }

            var staged = GetStagedChanges();
            var selectedStaged = new List<Models.Change>();
            foreach (var c in staged)
            {
                if (lastSelectedStaged.Contains(c.Path))
                    selectedStaged.Add(c);
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                _isLoadingData = true;
                Unstaged = unstaged;
                Staged = staged;
                SelectedUnstaged = selectedUnstaged;
                SelectedStaged = selectedStaged;
                _isLoadingData = false;

                if (selectedUnstaged.Count == 1)
                    SetDetail(selectedUnstaged[0], true);
                else if (selectedStaged.Count == 1)
                    SetDetail(selectedStaged[0], false);
                else
                    SetDetail(null, false);

                //// Try to load merge message from MERGE_MSG
                //if (string.IsNullOrEmpty(_commitMessage))
                //{
                //    var mergeMsgFile = Path.Combine(_repo.GitDir, "MERGE_MSG");
                //    if (File.Exists(mergeMsgFile))
                //        CommitMessage = File.ReadAllText(mergeMsgFile);
                //}
            });

            return hasConflict;
        }

        public void OpenAssumeUnchanged()
        {
            //App.OpenDialog(new Views.AssumeUnchangedManager()
            //{
            //    DataContext = new AssumeUnchangedManager(_repo.FullPath)
            //});
        }

        //public void StashAll(bool autoStart)
        //{
        //    if (!PopupHost.CanCreatePopup())
        //        return;

        //    if (autoStart)
        //        PopupHost.ShowAndStartPopup(new StashChanges(_repo, _cached, false));
        //    else
        //        PopupHost.ShowPopup(new StashChanges(_repo, _cached, false));
        //}

        public void StageSelected(Models.Change next)
        {
            StageChanges(_selectedUnstaged, next);
        }

        public void StageAll()
        {
            StageChanges(_unstaged, null);
        }

        public async void StageChanges(List<Models.Change> changes, Models.Change next)
        {
            if (_unstaged.Count == 0 || changes.Count == 0)
                return;

            // Use `_selectedUnstaged` instead of `SelectedUnstaged` to avoid UI refresh.
            _selectedUnstaged = next != null ? [next] : [];

            IsStaging = true;

            var group = changes.GroupBy(c => c.Repo);
            foreach (var g in group)
            {
                var repo = g.Key;
                changes = g.ToList();

                repo.SetWatcherEnabled(false);
                if (changes.Count == _unstaged.Count)
                {
                    await Task.Run(() => new Commands.Add(repo.FullPath, repo.IncludeUntracked).Exec());
                }
                else
                {
                    for (int i = 0; i < changes.Count; i += 10)
                    {
                        var count = Math.Min(10, changes.Count - i);
                        var step = changes.GetRange(i, count);
                        await Task.Run(() => new Commands.Add(repo.FullPath, step).Exec());
                    }
                }
                repo.MarkWorkingCopyDirtyManually();
                repo.SetWatcherEnabled(true);
            }

            IsStaging = false;

            Refresh();
        }

        public void UnstageSelected(Models.Change next)
        {
            UnstageChanges(_selectedStaged, next);
        }

        public void UnstageAll()
        {
            UnstageChanges(_staged, null);
        }

        public async void UnstageChanges(List<Models.Change> changes, Models.Change next)
        {
            if (_staged.Count == 0 || changes.Count == 0)
                return;

            // Use `_selectedStaged` instead of `SelectedStaged` to avoid UI refresh.
            _selectedStaged = next != null ? [next] : [];

            IsUnstaging = true;

            var group = changes.GroupBy(c => c.Repo);
            foreach (var g in group)
            {
                var repo = g.Key;
                changes = g.ToList();

                repo.SetWatcherEnabled(false);
                if (_useAmend)
                {
                    await Task.Run(() => new Commands.UnstageChangesForAmend(repo.FullPath, changes).Exec());
                }
                else if (changes.Count == _staged.Count)
                {
                    await Task.Run(() => new Commands.Reset(repo.FullPath).Exec());
                }
                else
                {
                    for (int i = 0; i < changes.Count; i += 10)
                    {
                        var count = Math.Min(10, changes.Count - i);
                        var step = changes.GetRange(i, count);
                        await Task.Run(() => new Commands.Reset(repo.FullPath, step).Exec());
                    }
                }
                repo.MarkWorkingCopyDirtyManually();
                repo.SetWatcherEnabled(true);
            }

            IsUnstaging = false;

            Refresh();
        }

        public void Discard(List<Models.Change> changes)
        {
            if (PopupHost.CanCreatePopup())
            {
                PopupHost.ShowPopup(new Discard(null, changes));
            }

            Refresh();
        }

        public void Commit()
        {
            DoCommit(AutoStageBeforeCommit, false);
        }

        public void Refresh()
        {
            _group.MarkWorkingCopyDirtyManually();
        }

        public void CommitWithAutoStage()
        {
            DoCommit(true, false);
        }

        public void CommitWithPush()
        {
            DoCommit(AutoStageBeforeCommit, true);
        }

        public ContextMenu CreateContextMenuForUnstagedChanges()
        {
            if (_selectedUnstaged == null || _selectedUnstaged.Count == 0)
                return null;

            var menu = new ContextMenu();
            if (_selectedUnstaged.Count == 1)
            {
                var change = _selectedUnstaged[0];
                var path = Path.GetFullPath(Path.Combine(change.Repo.FullPath, change.Path));

                var explore = new MenuItem();
                explore.Header = App.Text("RevealFile");
                explore.Icon = App.CreateMenuIcon("Icons.Explore");
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Click += (_, e) =>
                {
                    Native.OS.OpenInFileManager(path, true);
                    e.Handled = true;
                };
                menu.Items.Add(explore);

                var openWith = new MenuItem();
                openWith.Header = App.Text("OpenWith");
                openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openWith.IsEnabled = File.Exists(path);
                openWith.Click += (_, e) =>
                {
                    Native.OS.OpenWithDefaultEditor(path);
                    e.Handled = true;
                };
                menu.Items.Add(openWith);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (change.IsConflit)
                {
                    var useTheirs = new MenuItem();
                    useTheirs.Icon = App.CreateMenuIcon("Icons.Incoming");
                    useTheirs.Header = App.Text("FileCM.UseTheirs");
                    useTheirs.Click += (_, e) =>
                    {
                        UseTheirs(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = App.CreateMenuIcon("Icons.Local");
                    useMine.Header = App.Text("FileCM.UseMine");
                    useMine.Click += (_, e) =>
                    {
                        UseMine(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var openMerger = new MenuItem();
                    openMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                    openMerger.Header = App.Text("FileCM.OpenWithExternalMerger");
                    openMerger.Click += (_, e) =>
                    {
                        UseExternalMergeTool(change);
                        e.Handled = true;
                    };

                    menu.Items.Add(useTheirs);
                    menu.Items.Add(useMine);
                    menu.Items.Add(openMerger);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                else
                {
                    var stage = new MenuItem();
                    stage.Header = App.Text("FileCM.Stage");
                    stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                    stage.Click += (_, e) =>
                    {
                        StageChanges(_selectedUnstaged, null);
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.Discard");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Click += (_, e) =>
                    {
                        Discard(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var assumeUnchanged = new MenuItem();
                    assumeUnchanged.Header = App.Text("FileCM.AssumeUnchanged");
                    assumeUnchanged.Icon = App.CreateMenuIcon("Icons.File.Ignore");
                    assumeUnchanged.IsVisible = change.WorkTree != Models.ChangeState.Untracked;
                    assumeUnchanged.Click += (_, e) =>
                    {
                        new Commands.AssumeUnchanged(change.Repo.FullPath).Add(change.Path);
                        e.Handled = true;
                    };

                    var history = new MenuItem();
                    history.Header = App.Text("FileHistory");
                    history.Icon = App.CreateMenuIcon("Icons.Histories");
                    history.Click += (_, e) =>
                    {
                        var window = new Views.FileHistories() { DataContext = new FileHistories(change.Repo, change.Path) };
                        window.Show();
                        e.Handled = true;
                    };

                    menu.Items.Add(stage);
                    menu.Items.Add(discard);
                    menu.Items.Add(assumeUnchanged);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(history);
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    var extension = Path.GetExtension(change.Path);
                    var hasExtra = false;
                    if (change.WorkTree == Models.ChangeState.Untracked)
                    {
                        var isRooted = change.Path.IndexOf('/', StringComparison.Ordinal) <= 0;
                        var addToIgnore = new MenuItem();
                        addToIgnore.Header = App.Text("WorkingCopy.AddToGitIgnore");
                        addToIgnore.Icon = App.CreateMenuIcon("Icons.GitIgnore");

                        var singleFile = new MenuItem();
                        singleFile.Header = App.Text("WorkingCopy.AddToGitIgnore.SingleFile");
                        singleFile.Click += (_, e) =>
                        {
                            Commands.GitIgnore.Add(change.Repo.FullPath, change.Path);
                            e.Handled = true;
                        };
                        addToIgnore.Items.Add(singleFile);

                        var byParentFolder = new MenuItem();
                        byParentFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.InSameFolder");
                        byParentFolder.IsVisible = !isRooted;
                        byParentFolder.Click += (_, e) =>
                        {
                            Commands.GitIgnore.Add(change.Repo.FullPath, Path.GetDirectoryName(change.Path) + "/");
                            e.Handled = true;
                        };
                        addToIgnore.Items.Add(byParentFolder);

                        if (!string.IsNullOrEmpty(extension))
                        {
                            var byExtension = new MenuItem();
                            byExtension.Header = App.Text("WorkingCopy.AddToGitIgnore.Extension", extension);
                            byExtension.Click += (_, e) =>
                            {
                                Commands.GitIgnore.Add(change.Repo.FullPath, "*" + extension);
                                e.Handled = true;
                            };
                            addToIgnore.Items.Add(byExtension);

                            var byExtensionInSameFolder = new MenuItem();
                            byExtensionInSameFolder.Header = App.Text("WorkingCopy.AddToGitIgnore.ExtensionInSameFolder", extension);
                            byExtensionInSameFolder.IsVisible = !isRooted;
                            byExtensionInSameFolder.Click += (_, e) =>
                            {
                                Commands.GitIgnore.Add(change.Repo.FullPath, Path.GetDirectoryName(change.Path) + "/*" + extension);
                                e.Handled = true;
                            };
                            addToIgnore.Items.Add(byExtensionInSameFolder);
                        }

                        menu.Items.Add(addToIgnore);
                        hasExtra = true;
                    }

                    var lfsEnabled = new Commands.LFS(change.Repo.FullPath).IsEnabled();
                    if (lfsEnabled)
                    {
                        var lfs = new MenuItem();
                        lfs.Header = App.Text("GitLFS");
                        lfs.Icon = App.CreateMenuIcon("Icons.LFS");

                        var isLFSFiltered = new Commands.IsLFSFiltered(change.Repo.FullPath, change.Path).Result();
                        if (!isLFSFiltered)
                        {
                            var filename = Path.GetFileName(change.Path);
                            var lfsTrackThisFile = new MenuItem();
                            lfsTrackThisFile.Header = App.Text("GitLFS.Track", filename);
                            lfsTrackThisFile.Click += async (_, e) =>
                            {
                                var succ = await Task.Run(() => new Commands.LFS(change.Repo.FullPath).Track(filename, true));
                                if (succ)
                                    App.SendNotification(change.Repo.FullPath, $"Tracking file named {filename} successfully!");

                                e.Handled = true;
                            };
                            lfs.Items.Add(lfsTrackThisFile);

                            if (!string.IsNullOrEmpty(extension))
                            {
                                var lfsTrackByExtension = new MenuItem();
                                lfsTrackByExtension.Header = App.Text("GitLFS.TrackByExtension", extension);
                                lfsTrackByExtension.Click += async (_, e) =>
                                {
                                    var succ = await Task.Run(() => new Commands.LFS(change.Repo.FullPath).Track("*" + extension));
                                    if (succ)
                                        App.SendNotification(change.Repo.FullPath, $"Tracking all *{extension} files successfully!");

                                    e.Handled = true;
                                };
                                lfs.Items.Add(lfsTrackByExtension);
                            }

                            lfs.Items.Add(new MenuItem() { Header = "-" });
                        }

                        var lfsLock = new MenuItem();
                        lfsLock.Header = App.Text("GitLFS.Locks.Lock");
                        lfsLock.Icon = App.CreateMenuIcon("Icons.Lock");
                        lfsLock.IsEnabled = change.Repo.Remotes.Count > 0;
                        if (change.Repo.Remotes.Count == 1)
                        {
                            lfsLock.Click += async (_, e) =>
                            {
                                var succ = await Task.Run(() => new Commands.LFS(change.Repo.FullPath).Lock(change.Repo.Remotes[0].Name, change.Path));
                                if (succ)
                                    App.SendNotification(change.Repo.FullPath, $"Lock file \"{change.Path}\" successfully!");

                                e.Handled = true;
                            };
                        }
                        else
                        {
                            foreach (var remote in change.Repo.Remotes)
                            {
                                var remoteName = remote.Name;
                                var lockRemote = new MenuItem();
                                lockRemote.Header = remoteName;
                                lockRemote.Click += async (_, e) =>
                                {
                                    var succ = await Task.Run(() => new Commands.LFS(change.Repo.FullPath).Lock(remoteName, change.Path));
                                    if (succ)
                                        App.SendNotification(change.Repo.FullPath, $"Lock file \"{change.Path}\" successfully!");

                                    e.Handled = true;
                                };
                                lfsLock.Items.Add(lockRemote);
                            }
                        }
                        lfs.Items.Add(lfsLock);

                        var lfsUnlock = new MenuItem();
                        lfsUnlock.Header = App.Text("GitLFS.Locks.Unlock");
                        lfsUnlock.Icon = App.CreateMenuIcon("Icons.Unlock");
                        lfsUnlock.IsEnabled = change.Repo.Remotes.Count > 0;
                        if (change.Repo.Remotes.Count == 1)
                        {
                            lfsUnlock.Click += async (_, e) =>
                            {
                                var succ = await Task.Run(() => new Commands.LFS(change.Repo.FullPath).Unlock(change.Repo.Remotes[0].Name, change.Path, false));
                                if (succ)
                                    App.SendNotification(change.Repo.FullPath, $"Unlock file \"{change.Path}\" successfully!");

                                e.Handled = true;
                            };
                        }
                        else
                        {
                            foreach (var remote in change.Repo.Remotes)
                            {
                                var remoteName = remote.Name;
                                var unlockRemote = new MenuItem();
                                unlockRemote.Header = remoteName;
                                unlockRemote.Click += async (_, e) =>
                                {
                                    var succ = await Task.Run(() => new Commands.LFS(change.Repo.FullPath).Unlock(remoteName, change.Path, false));
                                    if (succ)
                                        App.SendNotification(change.Repo.FullPath, $"Unlock file \"{change.Path}\" successfully!");

                                    e.Handled = true;
                                };
                                lfsUnlock.Items.Add(unlockRemote);
                            }
                        }
                        lfs.Items.Add(lfsUnlock);

                        menu.Items.Add(lfs);
                        hasExtra = true;
                    }

                    if (hasExtra)
                        menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var copy = new MenuItem();
                copy.Header = App.Text("CopyPath");
                copy.Icon = App.CreateMenuIcon("Icons.Copy");
                copy.Click += (_, e) =>
                {
                    App.CopyText(change.Path);
                    e.Handled = true;
                };
                menu.Items.Add(copy);

                var copyFileName = new MenuItem();
                copyFileName.Header = App.Text("CopyFileName");
                copyFileName.Icon = App.CreateMenuIcon("Icons.Copy");
                copyFileName.Click += (_, e) =>
                {
                    App.CopyText(Path.GetFileName(change.Path));
                    e.Handled = true;
                };
                menu.Items.Add(copyFileName);
            }
            else
            {
                var hasConflicts = false;
                var hasNoneConflicts = false;
                foreach (var change in _selectedUnstaged)
                {
                    if (change.IsConflit)
                        hasConflicts = true;
                    else
                        hasNoneConflicts = true;
                }

                if (hasConflicts)
                {
                    if (hasNoneConflicts)
                    {
                        App.RaiseException(_group.PathPrefix, "You have selected both non-conflict changes with conflicts!");
                        return null;
                    }

                    var useTheirs = new MenuItem();
                    useTheirs.Icon = App.CreateMenuIcon("Icons.Incoming");
                    useTheirs.Header = App.Text("FileCM.UseTheirs");
                    useTheirs.Click += (_, e) =>
                    {
                        UseTheirs(_selectedUnstaged);
                        e.Handled = true;
                    };

                    var useMine = new MenuItem();
                    useMine.Icon = App.CreateMenuIcon("Icons.Local");
                    useMine.Header = App.Text("FileCM.UseMine");
                    useMine.Click += (_, e) =>
                    {
                        UseMine(_selectedUnstaged);
                        e.Handled = true;
                    };

                    menu.Items.Add(useTheirs);
                    menu.Items.Add(useMine);
                    return menu;
                }

                var stage = new MenuItem();
                stage.Header = App.Text("FileCM.StageMulti", _selectedUnstaged.Count);
                stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                stage.Click += (_, e) =>
                {
                    StageChanges(_selectedUnstaged, null);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.DiscardMulti", _selectedUnstaged.Count);
                discard.Icon = App.CreateMenuIcon("Icons.Undo");
                discard.Click += (_, e) =>
                {
                    Discard(_selectedUnstaged);
                    e.Handled = true;
                };

                menu.Items.Add(stage);
                menu.Items.Add(discard);
            }

            return menu;
        }

        public ContextMenu CreateContextMenuForStagedChanges()
        {
            if (_selectedStaged == null || _selectedStaged.Count == 0)
                return null;

            var menu = new ContextMenu();
            if (_selectedStaged.Count == 1)
            {
                var change = _selectedStaged[0];
                var path = Path.GetFullPath(Path.Combine(change.Repo.FullPath, change.Path));

                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Icon = App.CreateMenuIcon("Icons.Explore");
                explore.Click += (_, e) =>
                {
                    Native.OS.OpenInFileManager(path, true);
                    e.Handled = true;
                };

                var openWith = new MenuItem();
                openWith.Header = App.Text("OpenWith");
                openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openWith.IsEnabled = File.Exists(path);
                openWith.Click += (_, e) =>
                {
                    Native.OS.OpenWithDefaultEditor(path);
                    e.Handled = true;
                };

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.Unstage");
                unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                unstage.Click += (_, e) =>
                {
                    UnstageChanges(_selectedStaged, null);
                    e.Handled = true;
                };

                //var stash = new MenuItem();
                //stash.Header = App.Text("FileCM.Stash");
                //stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
                //stash.Click += (_, e) =>
                //{
                //    if (PopupHost.CanCreatePopup())
                //        PopupHost.ShowPopup(new StashChanges(_repo, _selectedStaged, true));

                //    e.Handled = true;
                //};

                //var patch = new MenuItem();
                //patch.Header = App.Text("FileCM.SaveAsPatch");
                //patch.Icon = App.CreateMenuIcon("Icons.Diff");
                //patch.Click += async (_, e) =>
                //{
                //    var storageProvider = App.GetStorageProvider();
                //    if (storageProvider == null)
                //        return;

                //    var options = new FilePickerSaveOptions();
                //    options.Title = App.Text("FileCM.SaveAsPatch");
                //    options.DefaultExtension = ".patch";
                //    options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                //    var storageFile = await storageProvider.SaveFilePickerAsync(options);
                //    if (storageFile != null)
                //    {
                //        var succ = await Task.Run(() => Commands.SaveChangesAsPatch.Exec(_repo.FullPath, _selectedStaged, false, storageFile.Path.LocalPath));
                //        if (succ)
                //            App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                //    }

                //    e.Handled = true;
                //};

                var history = new MenuItem();
                history.Header = App.Text("FileHistory");
                history.Icon = App.CreateMenuIcon("Icons.Histories");
                history.Click += (_, e) =>
                {
                    var window = new Views.FileHistories() { DataContext = new FileHistories(change.Repo, change.Path) };
                    window.Show();
                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyPath.Click += (_, e) =>
                {
                    App.CopyText(change.Path);
                    e.Handled = true;
                };

                var copyFileName = new MenuItem();
                copyFileName.Header = App.Text("CopyFileName");
                copyFileName.Icon = App.CreateMenuIcon("Icons.Copy");
                copyFileName.Click += (_, e) =>
                {
                    App.CopyText(Path.GetFileName(change.Path));
                    e.Handled = true;
                };

                menu.Items.Add(explore);
                menu.Items.Add(openWith);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(unstage);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(history);
                menu.Items.Add(new MenuItem() { Header = "-" });

                //var lfsEnabled = new Commands.LFS(_repo.FullPath).IsEnabled();
                //if (lfsEnabled)
                //{
                //    var lfs = new MenuItem();
                //    lfs.Header = App.Text("GitLFS");
                //    lfs.Icon = App.CreateMenuIcon("Icons.LFS");

                //    var lfsLock = new MenuItem();
                //    lfsLock.Header = App.Text("GitLFS.Locks.Lock");
                //    lfsLock.Icon = App.CreateMenuIcon("Icons.Lock");
                //    lfsLock.IsEnabled = _repo.Remotes.Count > 0;
                //    if (_repo.Remotes.Count == 1)
                //    {
                //        lfsLock.Click += async (_, e) =>
                //        {
                //            var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Lock(_repo.Remotes[0].Name, change.Path));
                //            if (succ)
                //                App.SendNotification(_repo.FullPath, $"Lock file \"{change.Path}\" successfully!");

                //            e.Handled = true;
                //        };
                //    }
                //    else
                //    {
                //        foreach (var remote in _repo.Remotes)
                //        {
                //            var remoteName = remote.Name;
                //            var lockRemote = new MenuItem();
                //            lockRemote.Header = remoteName;
                //            lockRemote.Click += async (_, e) =>
                //            {
                //                var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Lock(remoteName, change.Path));
                //                if (succ)
                //                    App.SendNotification(_repo.FullPath, $"Lock file \"{change.Path}\" successfully!");

                //                e.Handled = true;
                //            };
                //            lfsLock.Items.Add(lockRemote);
                //        }
                //    }
                //    lfs.Items.Add(lfsLock);

                //    var lfsUnlock = new MenuItem();
                //    lfsUnlock.Header = App.Text("GitLFS.Locks.Unlock");
                //    lfsUnlock.Icon = App.CreateMenuIcon("Icons.Unlock");
                //    lfsUnlock.IsEnabled = _repo.Remotes.Count > 0;
                //    if (_repo.Remotes.Count == 1)
                //    {
                //        lfsUnlock.Click += async (_, e) =>
                //        {
                //            var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Unlock(_repo.Remotes[0].Name, change.Path, false));
                //            if (succ)
                //                App.SendNotification(_repo.FullPath, $"Unlock file \"{change.Path}\" successfully!");

                //            e.Handled = true;
                //        };
                //    }
                //    else
                //    {
                //        foreach (var remote in _repo.Remotes)
                //        {
                //            var remoteName = remote.Name;
                //            var unlockRemote = new MenuItem();
                //            unlockRemote.Header = remoteName;
                //            unlockRemote.Click += async (_, e) =>
                //            {
                //                var succ = await Task.Run(() => new Commands.LFS(_repo.FullPath).Unlock(remoteName, change.Path, false));
                //                if (succ)
                //                    App.SendNotification(_repo.FullPath, $"Unlock file \"{change.Path}\" successfully!");

                //                e.Handled = true;
                //            };
                //            lfsUnlock.Items.Add(unlockRemote);
                //        }
                //    }
                //    lfs.Items.Add(lfsUnlock);

                //    menu.Items.Add(lfs);
                //    menu.Items.Add(new MenuItem() { Header = "-" });
                //}

                menu.Items.Add(copyPath);
                menu.Items.Add(copyFileName);
            }
            else
            {
                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.UnstageMulti", _selectedStaged.Count);
                unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                unstage.Click += (_, e) =>
                {
                    UnstageChanges(_selectedStaged, null);
                    e.Handled = true;
                };

                //var stash = new MenuItem();
                //stash.Header = App.Text("FileCM.StashMulti", _selectedStaged.Count);
                //stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
                //stash.Click += (_, e) =>
                //{
                //    if (PopupHost.CanCreatePopup())
                //        PopupHost.ShowPopup(new StashChanges(_repo, _selectedStaged, true));

                //    e.Handled = true;
                //};

                //var patch = new MenuItem();
                //patch.Header = App.Text("FileCM.SaveAsPatch");
                //patch.Icon = App.CreateMenuIcon("Icons.Diff");
                //patch.Click += async (_, e) =>
                //{
                //    var storageProvider = App.GetStorageProvider();
                //    if (storageProvider == null)
                //        return;

                //    var options = new FilePickerSaveOptions();
                //    options.Title = App.Text("FileCM.SaveAsPatch");
                //    options.DefaultExtension = ".patch";
                //    options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                //    var storageFile = await storageProvider.SaveFilePickerAsync(options);
                //    if (storageFile != null)
                //    {
                //        var succ = await Task.Run(() => Commands.SaveChangesAsPatch.Exec(_repo.FullPath, _selectedStaged, false, storageFile.Path.LocalPath));
                //        if (succ)
                //            App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                //    }

                //    e.Handled = true;
                //};

                menu.Items.Add(unstage);
                //menu.Items.Add(stash);
                //menu.Items.Add(patch);
            }

            return menu;
        }

        public ContextMenu CreateContextMenuForCommitMessages()
        {
            var menu = new ContextMenu();

            var repo = _repos.First();

            var templateCount = repo.Settings.CommitTemplates.Count;
            if (templateCount == 0)
            {
                menu.Items.Add(new MenuItem()
                {
                    Header = App.Text("WorkingCopy.NoCommitTemplates"),
                    Icon = App.CreateMenuIcon("Icons.Code"),
                    IsEnabled = false
                });
            }
            else
            {
                for (int i = 0; i < templateCount; i++)
                {
                    var template = repo.Settings.CommitTemplates[i];
                    var item = new MenuItem();
                    item.Header = new Views.NameHighlightedTextBlock("WorkingCopy.UseCommitTemplate", template.Name);
                    item.Icon = App.CreateMenuIcon("Icons.Code");
                    item.Click += (_, e) =>
                    {
                        CommitMessage = template.Apply(repo.CurrentBranch, _staged);
                        e.Handled = true;
                    };
                    menu.Items.Add(item);
                }
            }

            menu.Items.Add(new MenuItem() { Header = "-" });

            var historiesCount = repo.Settings.CommitMessages.Count;
            if (historiesCount == 0)
            {
                menu.Items.Add(new MenuItem()
                {
                    Header = App.Text("WorkingCopy.NoCommitHistories"),
                    Icon = App.CreateMenuIcon("Icons.Histories"),
                    IsEnabled = false
                });
            }
            else
            {
                for (int i = 0; i < historiesCount; i++)
                {
                    var message = repo.Settings.CommitMessages[i];
                    var item = new MenuItem();
                    item.Header = message;
                    item.Icon = App.CreateMenuIcon("Icons.Histories");
                    item.Click += (_, e) =>
                    {
                        CommitMessage = message;
                        e.Handled = true;
                    };

                    menu.Items.Add(item);
                }
            }

            return menu;
        }

        private List<Models.Change> GetStagedChanges()
        {
            var rs = new List<Models.Change>();
            foreach (var c in _cached)
            {
                if (c.Index != Models.ChangeState.None &&
                    c.Index != Models.ChangeState.Untracked)
                    rs.Add(c);
            }
            return rs;
        }

        private void SetDetail(Models.Change change, bool isUnstaged)
        {
            if (_isLoadingData)
                return;

            if (change == null)
                DetailContext = null;
            else if (change.IsConflit && isUnstaged)
                DetailContext = new Conflict(change.Repo, change.Repo.WorkingCopy, change);
            else
                DetailContext = new DiffContext(change.Repo.FullPath, new Models.DiffOption(change, isUnstaged), _detailContext as DiffContext);
        }

        public void OpenTerminal()
        {
            if (!ViewModels.Preference.Instance.IsGitConfigured())
                App.RaiseException(PopupHost.Active.GetId(), App.Text("NotConfigured"));
            else
                Native.OS.OpenTerminal(null);
        }

        private async void UseTheirs(List<Models.Change> changes)
        {
            var files = new List<string>();
            foreach (var change in changes)
            {
                if (change.IsConflit)
                    files.Add(change.Path);
            }

            foreach (var g in changes.GroupBy(c => c.Repo))
            {
                var repo = g.Key;
                changes = g.ToList();

                repo.SetWatcherEnabled(false);
                var succ = await Task.Run(() => new Commands.Checkout(repo.FullPath).UseTheirs(files));
                if (succ)
                {
                    await Task.Run(() => new Commands.Add(repo.FullPath, changes).Exec());
                }
                repo.MarkWorkingCopyDirtyManually();
                repo.SetWatcherEnabled(true);
            }
        }

        private async void UseMine(List<Models.Change> changes)
        {
            var files = new List<string>();
            foreach (var change in changes)
            {
                if (change.IsConflit)
                    files.Add(change.Path);
            }

            foreach (var g in changes.GroupBy(c => c.Repo))
            {
                var repo = g.Key;
                changes = g.ToList();

                repo.SetWatcherEnabled(false);
                var succ = await Task.Run(() => new Commands.Checkout(repo.FullPath).UseMine(files));
                if (succ)
                {
                    await Task.Run(() => new Commands.Add(repo.FullPath, changes).Exec());
                }
                repo.MarkWorkingCopyDirtyManually();
                repo.SetWatcherEnabled(true);
            }
        }

        private async void UseExternalMergeTool(Models.Change change)
        {
            var toolType = Preference.Instance.ExternalMergeToolType;
            var toolPath = Preference.Instance.ExternalMergeToolPath;

            change.Repo.SetWatcherEnabled(false);
            await Task.Run(() => Commands.MergeTool.OpenForMerge(change.Repo.FullPath, toolType, toolPath, change.Path));
            change.Repo.SetWatcherEnabled(true);
        }



        private void DoCommit(bool autoStage, bool autoPush)
        {
            var group = Staged.GroupBy(c => c.Repo);
            foreach (var g in group)
            {
                var repo = g.Key;

                if (!PopupHost.CanCreatePopup())
                {
                    App.RaiseException(repo.FullPath, "Repository has unfinished job! Please wait!");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_commitMessage))
                {
                    App.RaiseException(repo.FullPath, "Commit without message is NOT allowed!");
                    return;
                }

                if (!_useAmend)
                {
                    if (autoStage)
                    {
                        if (_count == 0)
                        {
                            App.RaiseException(repo.FullPath, "No files added to commit!");
                            return;
                        }
                    }
                    else
                    {
                        if (_staged.Count == 0)
                        {
                            App.RaiseException(repo.FullPath, "No files added to commit!");
                            return;
                        }
                    }
                }

                IsCommitting = true;
                repo.Settings.PushCommitMessage(_commitMessage);
                repo.SetWatcherEnabled(false);

                var succ = true;
                if (autoStage && _unstaged.Count > 0)
                    succ = new Commands.Add(repo.FullPath, repo.IncludeUntracked).Exec();

                if (succ)
                    succ = new Commands.Commit(repo.FullPath, _commitMessage, _useAmend, repo.Settings.EnableSignOffForCommit).Exec();

                Dispatcher.UIThread.Post(() =>
                {
                    if (succ)
                    {
                        CommitMessage = string.Empty;

                        if (autoPush)
                            PopupHost.ShowAndStartPopup(new Push(repo, null));
                    }

                    repo.MarkBranchesDirtyManually();
                    repo.SetWatcherEnabled(true);
                    IsCommitting = false;
                });
            }
        }

        private bool IsChanged(List<Models.Change> old, List<Models.Change> cur)
        {
            if (old.Count != cur.Count)
                return true;

            var oldSet = new HashSet<string>();
            foreach (var c in old)
                oldSet.Add($"{c.Path}\n{c.WorkTree}\n{c.Index}");

            foreach (var c in cur)
            {
                if (!oldSet.Contains($"{c.Path}\n{c.WorkTree}\n{c.Index}"))
                    return true;
            }

            return false;
        }

        private List<Repository> _repos = null;
        private RepositoryGroup _group = null;
        private bool _isLoadingData = false;
        private bool _isStaging = false;
        private bool _isUnstaging = false;
        private bool _isCommitting = false;
        private bool _useAmend = false;
        private bool _canCommitWithPush = false;
        private List<Models.Change> _cached = [];
        private List<Models.Change> _unstaged = [];
        private List<Models.Change> _staged = [];
        private List<Models.Change> _selectedUnstaged = [];
        private List<Models.Change> _selectedStaged = [];
        private int _count = 0;
        private object _detailContext = null;
        private string _commitMessage = string.Empty;
        private bool _includeUntracked;
        private bool _autoStageBeforeCommit;
    }
}