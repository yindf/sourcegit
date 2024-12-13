using System;
using System.Collections;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class PopupHost : ObservableObject
    {
        public static PopupHost Active
        {
            get;
            set;
        } = null;

        private Queue<Popup> _queue = new Queue<Popup>();

        public Popup Popup
        {
            get => _popup;
            set => SetProperty(ref _popup, value);
        }

        public static bool CanCreatePopup()
        {
            return Active?.IsInProgress() != true;
        }

        public static void ShowPopup(Popup popup)
        {
            popup.HostPageId = Active.GetId();
            Active.Popup = popup;
            Active._queue.Clear();
        }

        public static void ShowAndStartPopup(Popup popup)
        {
            var dumpPage = Active;
            popup.HostPageId = dumpPage.GetId();

            if (dumpPage.Popup != null)
            {
                dumpPage._queue.Enqueue(popup);
                return;
            }

            dumpPage.Popup = popup;
            dumpPage.ProcessPopup();
        }

        public virtual string GetId()
        {
            return string.Empty;
        }

        public virtual bool IsInProgress()
        {
            return _popup is { InProgress: true };
        }

        public async void ProcessPopup()
        {
            if (_popup != null)
            {
                if (!_popup.Check())
                    return;

                _popup.InProgress = true;
                var task = _popup.Sure();
                if (task != null)
                {
                    var finished = await task;
                    _popup.InProgress = false;
                    if (finished)
                    {
                        if (_queue.TryDequeue(out var popup))
                        {
                            Popup = popup;
                            ProcessPopup();
                        }
                        else
                        {
                            Popup = null;
                        }
                    }
                }
                else
                {
                    _popup.InProgress = false;
                    if (_queue.TryDequeue(out var popup))
                    {
                        Popup = popup;
                        ProcessPopup();
                    }
                    else
                    {
                        Popup = null;
                    }
                }
            }
        }

        public void CancelPopup()
        {
            if (_popup == null)
                return;
            if (_popup.InProgress)
                return;

            _queue.Clear();
            Popup = null;
        }

        private Popup _popup = null;
    }
}
