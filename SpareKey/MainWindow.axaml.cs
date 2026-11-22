using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SpareKey;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnHelloClick(object? sender, RoutedEventArgs e)
    {
        GreetingText.Text = "Hello, world!";
    }
}