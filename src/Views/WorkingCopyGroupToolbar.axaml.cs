using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using SourceGit.ViewModels;

namespace SourceGit.Views
{
    public partial class WorkingCopyGroupToolbar : UserControl
    {
        bool _canceling = false;
        bool _switchDataContext = false;

        public WorkingCopyGroupToolbar()
        {
            InitializeComponent();
            currentBranch.SelectionChanged += CurrentBranch_SelectionChanged;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext == null)
            {
                return;
            }

            var repos = (DataContext as ViewModels.WorkingCopyGroup).Group.Repositories;
            var commonBranchNames = repos.SelectMany(repo => repo.Branches).Select(branch => branch.Name).Distinct();

            _switchDataContext = true;
            currentBranch.ItemsSource = commonBranchNames;
            currentBranch.SelectedItem = repos.FirstOrDefault().CurrentBranch.Name;
            _switchDataContext = false;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
        }


        private void CurrentBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || e.RemovedItems.Count == 0)
            {
                return;
            }

            if (_switchDataContext)
            {
                return;
            }

            if (_canceling)
            {
                _canceling = false;
                return;
            }

            //var sameBranch = true;
            //foreach(var repo in (DataContext as ViewModels.WorkingCopyGroup).Group.Repositories)
            //{
            //    if(repo.CurrentBranch.Name != e.AddedItems[0] as string)
            //    {
            //        sameBranch = false;
            //        break;
            //    }
            //}

            //if (sameBranch)
            //{
            //    return;
            //}


            var repos = (DataContext as ViewModels.WorkingCopyGroup).Group.Repositories;

            var dialog = new ConfirmCheckout();
            dialog.OnCancel = () => 
            {
                _canceling = true;
                if (e.RemovedItems.Count > 0)
                {
                    currentBranch.SelectedValue = e.RemovedItems[0];
                }
            };

            dialog.OnConfirm = () => 
            {
                foreach (var repo in repos)
                {
                    var checkout = new ViewModels.Checkout(repo, currentBranch.SelectedValue as string);
                    checkout.PreAction = Models.DealWithLocalChanges.Discard;
                    PopupHost.ShowAndStartPopup(checkout);
                }
            };
            
            App.OpenDialog(dialog);
        }

        public void Dispose()
        {
            if (currentBranch != null)
            {
                currentBranch.SelectionChanged -= CurrentBranch_SelectionChanged;
            }
        }
    }
}

