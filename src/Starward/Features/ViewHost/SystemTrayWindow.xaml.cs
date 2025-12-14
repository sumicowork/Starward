using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameLauncher;
using Starward.Features.Setting;
using Starward.Frameworks;
using Starward.Helpers;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Vanara.PInvoke;
using Windows.Foundation;


namespace Starward.Features.ViewHost;

[INotifyPropertyChanged]
public sealed partial class SystemTrayWindow : WindowEx
{

    public ObservableCollection<GameLaunchItem> InstalledGames { get; set => SetProperty(ref field, value); } = new();


    public SystemTrayWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
        SetTrayIcon();
        LoadInstalledGames();
        MenuStackPanel.DataContext = this;
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (_, _) => this.Bindings.Update());
    }




    private unsafe void InitializeWindow()
    {
        new SystemBackdropHelper(this, SystemBackdropProperty.AcrylicDefault with
        {
            TintColorLight = 0xFFE7E7E7,
            TintColorDark = 0xFF404040
        }).TrySetAcrylic(true);

        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += (s, e) => e.Cancel = true;
        this.Activated += SystemTrayWindow_Activated;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        var flag = User32.GetWindowLongPtr(WindowHandle, User32.WindowLongFlags.GWL_STYLE);
        flag &= ~(nint)User32.WindowStyles.WS_CAPTION;
        flag &= ~(nint)User32.WindowStyles.WS_BORDER;
        User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE, flag);
        var p = DwmApi.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
        DwmApi.DwmSetWindowAttribute(WindowHandle, DwmApi.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, (nint)(&p), sizeof(DwmApi.DWM_WINDOW_CORNER_PREFERENCE));

        Show();
        Hide();
    }



    private void SetTrayIcon()
    {
        try
        {
            string icon = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico");
            if (File.Exists(icon))
            {
                trayIcon.Icon = new(icon);
            }
        }
        catch { }
    }


    private void LoadInstalledGames()
    {
        try
        {
            InstalledGames.Clear();
            System.Diagnostics.Debug.WriteLine($"[SystemTray] Starting LoadInstalledGames, checking {GameBiz.AllGameBizs.Count()} game bizs");
            foreach (GameBiz gameBiz in GameBiz.AllGameBizs)
            {
                var gameId = GameId.FromGameBiz(gameBiz);
                System.Diagnostics.Debug.WriteLine($"[SystemTray] Checking {gameBiz}: GameId={gameId?.Id}");
                if (gameId != null)
                {
                    string? installPath = GameLauncherService.GetGameInstallPath(gameId);
                    System.Diagnostics.Debug.WriteLine($"[SystemTray] InstallPath for {gameBiz}: {installPath}, Exists={!string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath)}");
                    if (!string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath))
                    {
                        string displayName = $"{gameBiz.ToGameName()} - {gameBiz.ToGameServerName()}";
                        System.Diagnostics.Debug.WriteLine($"[SystemTray] Adding game: {displayName}");
                        InstalledGames.Add(new GameLaunchItem(gameId, displayName));
                    }
                }
            }
            System.Diagnostics.Debug.WriteLine($"[SystemTray] LoadInstalledGames completed, found {InstalledGames.Count} installed games");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemTray] Error in LoadInstalledGames: {ex}");
        }
    }




    private void SystemTrayWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState is WindowActivationState.Deactivated)
        {
            Hide();
        }
    }



    [RelayCommand]
    public override void Show()
    {
        RootGrid.RequestedTheme = ShouldSystemUseDarkMode() ? ElementTheme.Dark : ElementTheme.Light;
        RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SIZE windowSize = new()
        {
            Width = (int)(RootGrid.DesiredSize.Width * UIScale),
            Height = (int)(RootGrid.DesiredSize.Height * UIScale)
        };
        User32.GetCursorPos(out POINT point);
        User32.CalculatePopupWindowPosition(point, windowSize, User32.TrackPopupMenuFlags.TPM_RIGHTALIGN | User32.TrackPopupMenuFlags.TPM_BOTTOMALIGN | User32.TrackPopupMenuFlags.TPM_WORKAREA, null, out RECT windowPos);
        User32.MoveWindow(WindowHandle, windowPos.X, windowPos.Y, windowPos.Width, windowPos.Height, true);
        base.Show();
    }



    [RelayCommand]
    public override void Hide()
    {
        base.Hide();
    }



    [RelayCommand]
    public void ShowMainWindow()
    {
        App.Current.EnsureMainWindow();
    }


    [RelayCommand]
    private void Exit()
    {
        App.Current.Exit();
    }


    private void WindowEx_Closed(object sender, WindowEventArgs args)
    {
        trayIcon?.Dispose();
    }


}


[INotifyPropertyChanged]
public partial class GameLaunchItem
{
    public GameId GameId { get; }

    public string DisplayName { get; }

    public string GameIcon { get; }

    public GameLaunchItem(GameId gameId, string displayName)
    {
        GameId = gameId;
        DisplayName = displayName;
        GameIcon = GameBizToIcon(gameId.GameBiz);
    }

    private static string GameBizToIcon(GameBiz gameBiz)
    {
        return gameBiz.Game switch
        {
            GameBiz.bh3 => "ms-appx:///Assets/Image/icon_bh3.jpg",
            GameBiz.hk4e => "ms-appx:///Assets/Image/icon_ys.jpg",
            GameBiz.hkrpg => "ms-appx:///Assets/Image/icon_sr.jpg",
            GameBiz.nap => "ms-appx:///Assets/Image/icon_zzz.jpg",
            _ => "ms-appx:///Assets/Image/Transparent.png",
        };
    }

    [RelayCommand]
    private async void Launch()
    {
        try
        {
            var gameLauncherService = AppConfig.GetService<GameLauncherService>();
            await gameLauncherService.StartGameAsync(GameId);
        }
        catch { }
    }
}


