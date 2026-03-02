using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SystemTools.Views;

public partial class ExtendShutdownDialog : Window
{
    public int? ResultMinutes { get; private set; }

    public ExtendShutdownDialog()
    {
        InitializeComponent();
        ConfirmButton.Click += OnConfirmButtonClick;
        CancelButton.Click += OnCancelButtonClick;
    }

    public NumericUpDown MinutesInput => this.FindControl<NumericUpDown>(nameof(MinutesInput));
    public Button ConfirmButton => this.FindControl<Button>(nameof(ConfirmButton));
    public Button CancelButton => this.FindControl<Button>(nameof(CancelButton));

    private void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        ResultMinutes = (int)(MinutesInput.Value ?? 1);
        Close();
    }

    private void OnCancelButtonClick(object? sender, RoutedEventArgs e)
    {
        ResultMinutes = null;
        Close();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
