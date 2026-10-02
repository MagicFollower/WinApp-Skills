using System;
using PromptFavorites.ViewModels;

namespace PromptFavorites.Models
{
    public class PromptItem : ViewModelBase
    {
        private string _title;
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private string _module;
        public string Module
        {
            get { return _module; }
            set { SetProperty(ref _module, value); }
        }

        public string FilePath { get; set; }

        private bool _favorite;
        public bool Favorite
        {
            get { return _favorite; }
            set { SetProperty(ref _favorite, value); }
        }

        private int _useCount;
        public int UseCount
        {
            get { return _useCount; }
            set { SetProperty(ref _useCount, value); }
        }

        private DateTime _createdAt;
        public DateTime CreatedAt
        {
            get { return _createdAt; }
            set { SetProperty(ref _createdAt, value); }
        }

        private DateTime _updatedAt;
        public DateTime UpdatedAt
        {
            get { return _updatedAt; }
            set { SetProperty(ref _updatedAt, value); }
        }

        private DateTime? _lastUsedAt;
        public DateTime? LastUsedAt
        {
            get { return _lastUsedAt; }
            set { SetProperty(ref _lastUsedAt, value); }
        }

        private string _body;
        public string Body
        {
            get { return _body; }
            set { SetProperty(ref _body, value); }
        }
    }
}
