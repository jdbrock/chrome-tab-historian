using System.Windows;
using System.Windows.Input;
using TabHistorian.Viewer.ViewModels;

namespace TabHistorian.Viewer;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "TabHistorian Viewer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        SearchBox.Focus();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _viewModel?.ClearSearch();
            e.Handled = true;
        }
    }

    private void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ClearSearch();
        SearchBox.Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel?.Dispose();
        base.OnClosed(e);
    }
}
