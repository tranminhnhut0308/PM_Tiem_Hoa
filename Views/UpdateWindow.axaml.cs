using Avalonia.Controls;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class UpdateWindow : Window
{
    public UpdateWindow()
    {
        InitializeComponent();

        var vm = new UpdateViewModel();
        vm.CloseAction = () => Close();
        DataContext = vm;
    }
}
