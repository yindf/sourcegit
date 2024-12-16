﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RepositoryNode : ObservableObject
    {
        [JsonIgnore]
        public object Context { get; set; }
        public string Id
        {
            get => _id;
            set
            {
                var normalized = value.Replace('\\', '/');
                SetProperty(ref _id, normalized);
            }
        }

        public Repository Repo
        {
            get
            {
                if (_repo == null)
                {
                    if (App.GetLauncer().ActivePage.Data is RepositoryGroup group)
                    {
                        _repo = group.Repositories.Where(r => r.RepositoryNode.Id == Id).FirstOrDefault();
                    }
                }
                return _repo;
            }
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Bookmark
        {
            get => _bookmark;
            set => SetProperty(ref _bookmark, value);
        }

        public bool IsRepository
        {
            get => _isRepository;
            set => SetProperty(ref _isRepository, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        [JsonIgnore]
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        [JsonIgnore]
        public bool IsInvalid
        {
            get => _isRepository && !Directory.Exists(_id);
        }

        [JsonIgnore]
        public int Depth
        {
            get;
            set;
        } = 0;

        public List<RepositoryNode> SubNodes
        {
            get;
            set;
        } = [];

        public void Edit()
        {
            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new EditRepositoryNode(this));
        }

        public void AddSubFolder()
        {
            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new CreateGroup(this));
        }

        public void OpenInFileManager()
        {
            if (!IsRepository)
                return;
            Native.OS.OpenInFileManager(_id);
        }

        public void OpenTerminal()
        {
            if (!IsRepository)
                return;
            Native.OS.OpenTerminal(_id);
        }

        public void Delete()
        {
            if (PopupHost.CanCreatePopup())
                PopupHost.ShowPopup(new DeleteRepositoryNode(this));
        }

        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _displayName = string.Empty;
        private bool _isRepository = false;
        private int _bookmark = 0;
        private bool _isExpanded = false;
        private bool _isVisible = true;
        private bool _busy = true;
        private Repository _repo = null;
    }
}
