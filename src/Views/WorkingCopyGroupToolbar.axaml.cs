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
        public WorkingCopyGroupToolbar()
        {
            InitializeComponent();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            // Additional initialization logic if needed

            var repos = (DataContext as ViewModels.WorkingCopyGroup).Group.Repositories;
            var commonBranchNames = repos.Select(repo => repo.Branches.Select(branch => branch.Name).ToHashSet())
                .Aggregate((previousSet, nextSet) =>
                {
                    previousSet.IntersectWith(nextSet);
                    return previousSet;
                }).ToList();


            currentBranch.ItemsSource = commonBranchNames;
            currentBranch.SelectedIndex = commonBranchNames.IndexOf(repos.FirstOrDefault().CurrentBranch.Name);

            currentBranch.SelectionChanged += CurrentBranch_SelectionChanged;
        }

        bool canceling = false;

        private void CurrentBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (canceling)
            {
                return;
            }

            var repos = (DataContext as ViewModels.WorkingCopyGroup).Group.Repositories;

            var dialog = new ConfirmCheckout();
            dialog.OnCancel = () => 
            {
                canceling = true;
                currentBranch.SelectedValue = e.RemovedItems[0];
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

