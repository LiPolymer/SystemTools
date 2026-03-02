using Avalonia.Controls;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SystemTools.Settings;
using SystemTools.Views;

namespace SystemTools.Actions;

[ActionInfo("SystemTools.AdvancedShutdown", "高级计时关机", "\uE4C4", false)]
public class AdvancedShutdownAction(ILogger<AdvancedShutdownAction> logger) : ActionBase<AdvancedShutdownSettings>
{
    private readonly ILogger<AdvancedShutdownAction> _logger = logger;
    private readonly object _syncLock = new();
    private DateTimeOffset _shutdownAt = DateTimeOffset.MinValue;
    private int _totalScheduledSeconds;
    private Process? _countdownProcess;
    private AdvancedShutdownDialog? _activeDialog;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("AdvancedShutdownAction OnInvoke 开始");

        var configuredMinutes = Math.Max(1, Settings?.Minutes ?? 2);
        ScheduleShutdown(configuredMinutes);

        await ShowDialogAsync();
        await base.OnInvoke();
    }

    private void ScheduleShutdown(int minutes)
    {
        var safeMinutes = Math.Max(1, minutes);
        var seconds = safeMinutes * 60;

        lock (_syncLock)
        {
            _shutdownAt = DateTimeOffset.Now.AddMinutes(safeMinutes);
            _totalScheduledSeconds = seconds;
        }

        StartOrReplaceCountdownProcess(seconds);
    }

    private void ExtendShutdown(int extendMinutes)
    {
        var safeExtendMinutes = Math.Max(1, extendMinutes);
        DateTimeOffset targetTime;

        lock (_syncLock)
        {
            var baseline = _shutdownAt > DateTimeOffset.Now ? _shutdownAt : DateTimeOffset.Now;
            _shutdownAt = baseline.AddMinutes(safeExtendMinutes);
            targetTime = _shutdownAt;
            _totalScheduledSeconds = (int)Math.Ceiling((targetTime - DateTimeOffset.Now).TotalSeconds);
        }

        var totalSeconds = (int)Math.Ceiling((targetTime - DateTimeOffset.Now).TotalSeconds);
        totalSeconds = Math.Max(60, totalSeconds);
        StartOrReplaceCountdownProcess(totalSeconds);
    }

    private void CancelShutdownPlan()
    {
        StopCountdownProcess();
        lock (_syncLock)
        {
            _shutdownAt = DateTimeOffset.MinValue;
            _totalScheduledSeconds = 0;
        }
    }

    private void StartOrReplaceCountdownProcess(int seconds)
    {
        StopCountdownProcess();
        var safeSeconds = Math.Max(60, seconds);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c timeout /t {safeSeconds} /nobreak >nul & shutdown /s /t 0",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            _countdownProcess = Process.Start(psi);
            _logger.LogInformation("已启动 Windows 计时关机进程，{Seconds} 秒后执行关机。", safeSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动计时关机进程失败。秒数: {Seconds}", safeSeconds);
            throw;
        }
    }

    private void StopCountdownProcess()
    {
        try
        {
            if (_countdownProcess is { HasExited: false })
            {
                _countdownProcess.Kill(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "停止计时关机进程时发生异常。将继续执行后续流程。");
        }
        finally
        {
            _countdownProcess?.Dispose();
            _countdownProcess = null;
        }
    }

    private int GetRemainingSeconds()
    {
        lock (_syncLock)
        {
            var remainingSeconds = (int)Math.Ceiling((_shutdownAt - DateTimeOffset.Now).TotalSeconds);
            return Math.Max(0, remainingSeconds);
        }
    }

    private double BuildCountdownProgress()
    {
        int remaining;
        int total;
        lock (_syncLock)
        {
            remaining = Math.Max(0, (int)Math.Ceiling((_shutdownAt - DateTimeOffset.Now).TotalSeconds));
            total = _totalScheduledSeconds;
        }

        if (total <= 0)
        {
            return 0;
        }

        return Math.Clamp(remaining * 100.0 / total, 0, 100);
    }

    private string BuildCountdownText()
    {
        var remainingSeconds = GetRemainingSeconds();
        var minutes = remainingSeconds / 60;
        var seconds = remainingSeconds % 60;
        return $"将在{minutes}分{seconds:00}秒后关机……";
    }

    private async Task ShowDialogAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            _activeDialog?.Close();

            var dialog = new AdvancedShutdownDialog();
            _activeDialog = dialog;
            dialog.CountdownTextBlock.Text = BuildCountdownText();
            dialog.CountdownProgressBar.Value = BuildCountdownProgress();

            var countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            countdownTimer.Tick += (_, _) =>
            {
                dialog.CountdownTextBlock.Text = BuildCountdownText();
                dialog.CountdownProgressBar.Value = BuildCountdownProgress();
            };
            countdownTimer.Start();

            dialog.Closed += (_, _) =>
            {
                countdownTimer.Stop();
                if (ReferenceEquals(_activeDialog, dialog))
                {
                    _activeDialog = null;
                }
            };

            dialog.ReadButton.Click += (_, _) => dialog.Close();
            dialog.CancelPlanButton.Click += (_, _) =>
            {
                CancelShutdownPlan();
                dialog.Close();
            };

            dialog.ExtendButton.Click += async (_, _) =>
            {
                var extendMinutes = await ShowExtendInputDialogAsync(dialog);
                if (extendMinutes.HasValue)
                {
                    ExtendShutdown(extendMinutes.Value);
                    dialog.Close();
                }
            };

            dialog.Show();
            await Task.CompletedTask;
        });
    }

    private static async Task<int?> ShowExtendInputDialogAsync(Window owner)
    {
        var dialog = new ExtendShutdownDialog();
        await dialog.ShowDialog(owner);
        return dialog.ResultMinutes;
    }
}
