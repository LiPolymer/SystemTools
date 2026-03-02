using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SystemTools.Views;

public partial class AdvancedShutdownDialog : Window
{
    public AdvancedShutdownDialog()
    {
        InitializeComponent();
    }

    public TextBlock CountdownTextBlock => this.FindControl<TextBlock>(nameof(CountdownTextBlock));
    public ProgressBar CountdownProgressBar => this.FindControl<ProgressBar>(nameof(CountdownProgressBar));
    public Button ReadButton => this.FindControl<Button>(nameof(ReadButton));
    public Button CancelPlanButton => this.FindControl<Button>(nameof(CancelPlanButton));
    public Button ExtendButton => this.FindControl<Button>(nameof(ExtendButton));

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
