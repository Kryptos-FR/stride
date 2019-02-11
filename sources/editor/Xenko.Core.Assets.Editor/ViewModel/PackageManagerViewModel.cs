using Xenko.Core.Presentation.Collections;
using Xenko.Core.Presentation.ViewModel;

namespace Xenko.Core.Assets.Editor.ViewModel
{
    internal sealed class PackageManagerViewModel : DispatcherViewModel
    {
        private PackageDescriptionViewModel _currentPackage;
        private readonly IObservableCollection<PackageDescriptionViewModel> _packages = new ObservableList<PackageDescriptionViewModel>();

        public PackageManagerViewModel()
            : base(ViewModelServiceProvider.NullServiceProvider) // FIXME
        {

        }

        public PackageDescriptionViewModel CurrentPackage
        {
            get { return _currentPackage; }
            set { SetValue(ref _currentPackage, value); }
        }

        public IObservableCollection<PackageDescriptionViewModel> Packages
        {
            get { return _packages; }
        }
    }

    internal sealed class PackageDescriptionViewModel : DispatcherViewModel
    {
        private string _description;
        private object _icon;
        private string _version;

        public PackageDescriptionViewModel()
            : base(ViewModelServiceProvider.NullServiceProvider) // FIXME
        {

        }

        public string Description
        {
            get { return _description; }
            set { SetValue(ref _description, value); }
        }

        public object Icon
        {
            get { return _icon; }
            set { SetValue(ref _icon, value); }
        }

        public string Version
        {
            get { return _version; }
            set { SetValue(ref _version, value); }
        }
    }
}
