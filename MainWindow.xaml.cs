using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;
using Windows.System;

namespace Chess;

public sealed partial class MainWindow : Window
{
    public bool RedirectedToNewerVersion { get; }
    private Button? activeSectionButton;
    private AppWindow? appWindow;

    public MainWindow()
    {
        InitializeComponent();

        if (TryLaunchNewerVersion())
        {
            RedirectedToNewerVersion = true;
            Close();
            return;
        }

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "chessicon.ico"));
        RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);

        Title = "Chess v1.0.1";
        if (!RestoreLoggedInAccount())
        {
            ShowLoginPrompt();
        }
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.F11 && appWindow is not null)
        {
            appWindow.SetPresenter(appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen
                ? AppWindowPresenterKind.Overlapped
                : AppWindowPresenterKind.FullScreen);
            args.Handled = true;
        }
    }

    private string AccountsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ChessAccounts.json");
    private string CurrentAccountPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ChessCurrentAccount.txt");

    private void AccountButton_Click(object sender, RoutedEventArgs e)
    {
        AccountActions.Visibility = AccountActions.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        AccountList.Visibility = Visibility.Collapsed;
        SignOutConfirmation.Visibility = Visibility.Collapsed;
        SignOutButton.Visibility = Visibility.Visible;
    }

    private void AccountButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 58, 58, 58));
        }
    }

    private void AccountButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }
    }

    private void LogoHoverSurface_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        LogoHoverSurface.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 58, 58, 58));
    }

    private void LogoHoverSurface_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        LogoHoverSurface.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }

    private void SwitchAccount_Click(object sender, RoutedEventArgs e)
    {
        AccountList.Children.Clear();
        foreach (var account in LoadAccounts())
        {
            var accountButton = new Button
            {
                Content = account.Username,
                Tag = account.Username,
                Style = (Style)Application.Current.Resources["NavButtonStyle"],
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            accountButton.Click += AccountChoice_Click;
            AccountList.Children.Add(accountButton);
        }

        var manageAccountsButton = new Button
        {
            Content = "Manage Accounts",
            Style = (Style)Application.Current.Resources["QuietButtonStyle"],
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        manageAccountsButton.Click += ManageAccounts_Click;
        AccountList.Children.Add(manageAccountsButton);
        AccountList.Visibility = AccountList.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void AccountChoice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string username })
        {
            LogInAccount(username);
            AccountActions.Visibility = Visibility.Collapsed;
        }
    }

    private void ManageAccounts_Click(object sender, RoutedEventArgs e)
    {
        AccountOverlay.Visibility = Visibility.Visible;
        AccountTabs.Visibility = Visibility.Visible;
        ShowSignUp_Click(sender, e);
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        SignOutButton.Visibility = Visibility.Collapsed;
        SignOutConfirmation.Visibility = Visibility.Visible;
        AccountList.Visibility = Visibility.Collapsed;
    }

    private void ConfirmSignOut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            File.Delete(CurrentAccountPath);
        }
        catch (IOException)
        {
        }

        AccountActions.Visibility = Visibility.Collapsed;
        ShowLoginPrompt();
    }

    private void CancelSignOut_Click(object sender, RoutedEventArgs e)
    {
        SignOutConfirmation.Visibility = Visibility.Collapsed;
        SignOutButton.Visibility = Visibility.Visible;
    }

    private void Home_Click(object sender, RoutedEventArgs e) => ShowMenu(HomeContent);

    private void Puzzles_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent, PuzzlesButton);

    private void Learn_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent, LearnButton);

    private void Watch_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent, WatchButton);

    private void Settings_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent, SettingsButton);

    private void TimeControl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button selectedButton)
        {
            return;
        }

        var inactiveBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 34, 34));
        var inactiveBorder = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        var activeBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 43, 37));
        var activeBorder = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 134, 196, 64));
        BlitzButton.Background = inactiveBackground;
        BlitzButton.BorderBrush = inactiveBorder;
        RapidButton.Background = inactiveBackground;
        RapidButton.BorderBrush = inactiveBorder;
        BulletButton.Background = inactiveBackground;
        BulletButton.BorderBrush = inactiveBorder;
        selectedButton.Background = activeBackground;
        selectedButton.BorderBrush = activeBorder;
    }

    private void CreateAccountPrompt_Click(object sender, RoutedEventArgs e) => ShowSignUp_Click(sender, e);

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        PlaySubmenu.Visibility = PlaySubmenu.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (activeSectionButton == OnlineButton || activeSectionButton == BotsButton || activeSectionButton == InPersonButton || activeSectionButton == SpecialButton)
        {
            SetActiveNavigation(activeSectionButton);
        }
        else
        {
            activeSectionButton = PlayButton;
            SetActiveNavigation(PlayButton);
        }
    }

    private void Online_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent, OnlineButton);

    private void Bots_Click(object sender, RoutedEventArgs e) => ShowMenu(BotsContent, BotsButton);

    private void InPerson_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent, InPersonButton);

    private void Special_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent, SpecialButton);

    private void ShowMenu(UIElement selectedContent, Button? selectedButton = null)
    {
        HomeContent.Visibility = selectedContent == HomeContent ? Visibility.Visible : Visibility.Collapsed;
        BotsContent.Visibility = selectedContent == BotsContent ? Visibility.Visible : Visibility.Collapsed;
        EngineContent.Visibility = selectedContent == BotsContent ? Visibility.Visible : Visibility.Collapsed;
        EmptyMenuContent.Visibility = selectedContent == EmptyMenuContent ? Visibility.Visible : Visibility.Collapsed;
        SetActiveNavigation(selectedButton ?? (selectedContent == HomeContent ? HomeButton : PlayButton));
    }

    private void SetActiveNavigation(Button selectedButton)
    {
        activeSectionButton = selectedButton;
        var inactiveBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        var activeBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 37, 32));
        HomeButton.Background = inactiveBrush;
        PlayButton.Background = inactiveBrush;
        OnlineButton.Background = inactiveBrush;
        BotsButton.Background = inactiveBrush;
        InPersonButton.Background = inactiveBrush;
        SpecialButton.Background = inactiveBrush;
        PuzzlesButton.Background = inactiveBrush;
        LearnButton.Background = inactiveBrush;
        WatchButton.Background = inactiveBrush;
        SettingsButton.Background = inactiveBrush;
        selectedButton.Background = activeBrush;
        if (selectedButton == OnlineButton || selectedButton == BotsButton || selectedButton == InPersonButton || selectedButton == SpecialButton)
        {
            PlayButton.Background = activeBrush;
        }
    }

    private void CloseAccount_Click(object sender, RoutedEventArgs e) => AccountOverlay.Visibility = Visibility.Collapsed;

    private void ShowSignUp_Click(object sender, RoutedEventArgs e)
    {
        SignUpFields.Visibility = Visibility.Visible;
        LogInFields.Visibility = Visibility.Collapsed;
        SignUpTab.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 54, 54, 54));
        LogInTab.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
        AccountErrorText.Text = string.Empty;
    }

    private void ShowLogIn_Click(object sender, RoutedEventArgs e)
    {
        SignUpFields.Visibility = Visibility.Collapsed;
        LogInFields.Visibility = Visibility.Visible;
        SignUpTab.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
        LogInTab.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 54, 54, 54));
        AccountErrorText.Text = string.Empty;
    }

    private void CreateAccount_Click(object sender, RoutedEventArgs e)
    {
        var username = SignUpUsername.Text.Trim();
        if (username.Length < 3 || username.Contains(' '))
        {
            AccountErrorText.Text = "Choose a username with at least 3 characters and no spaces.";
            return;
        }

        if (SignUpPassword.Password.Length < 6)
        {
            AccountErrorText.Text = "Your password must be at least 6 characters.";
            return;
        }

        if (SignUpPassword.Password != SignUpConfirmation.Password)
        {
            AccountErrorText.Text = "The passwords do not match.";
            return;
        }

        var accounts = LoadAccounts();
        if (accounts.Exists(account => string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase)))
        {
            AccountErrorText.Text = "That username already exists. Try logging in.";
            return;
        }

        accounts.Add(new LocalAccount { Username = username, PasswordHash = HashPassword(SignUpPassword.Password) });
        SaveAccounts(accounts);
        LogInAccount(username);
    }

    private void LogIn_Click(object sender, RoutedEventArgs e)
    {
        var username = LogInUsername.Text.Trim();
        var account = LoadAccounts().Find(candidate => string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase));
        if (account is null || account.PasswordHash != HashPassword(LogInPassword.Password))
        {
            AccountErrorText.Text = "That username or password is not recognized.";
            return;
        }

        LogInAccount(account.Username);
    }

    private void LogInAccount(string username)
    {
        AccountNameText.Text = username;
        AccountStatusText.Text = "Local account";
        AccountAvatar.Text = username[..1].ToUpperInvariant();
        AccountOverlay.Visibility = Visibility.Collapsed;
        ShowMenu(HomeContent);
        SaveCurrentAccount(username);
    }

    private void ShowLoginPrompt()
    {
        AccountOverlay.Visibility = Visibility.Visible;
        AccountTabs.Visibility = Visibility.Collapsed;
        ShowLogIn_Click(this, new RoutedEventArgs());
    }

    private bool RestoreLoggedInAccount()
    {
        try
        {
            if (!File.Exists(CurrentAccountPath))
            {
                return false;
            }

            var username = File.ReadAllText(CurrentAccountPath).Trim();
            var account = LoadAccounts().Find(candidate => string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase));
            if (account is not null)
            {
                LogInAccount(account.Username);
                return true;
            }
        }
        catch (IOException)
        {
        }

        return false;
    }

    private void SaveCurrentAccount(string username)
    {
        try
        {
            File.WriteAllText(CurrentAccountPath, username);
        }
        catch (IOException)
        {
        }
    }

    private List<LocalAccount> LoadAccounts()
    {
        try
        {
            if (File.Exists(AccountsPath))
            {
                return JsonSerializer.Deserialize<List<LocalAccount>>(File.ReadAllText(AccountsPath)) ?? new List<LocalAccount>();
            }
        }
        catch (JsonException)
        {
        }

        return new List<LocalAccount>();
    }

    private void SaveAccounts(List<LocalAccount> accounts)
    {
        var json = JsonSerializer.Serialize(accounts, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AccountsPath, json);
    }

    private static string HashPassword(string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hash);
    }

    private sealed class LocalAccount
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }

    private static bool TryLaunchNewerVersion()
    {
        var currentPath = Environment.ProcessPath;
        var projectRoot = FindProjectRoot();
        if (currentPath is null || projectRoot is null)
        {
            return false;
        }

        var currentWriteTime = File.GetLastWriteTimeUtc(currentPath);
        var newerBuild = Directory.EnumerateFiles(Path.Combine(projectRoot, "bin"), "Chess.exe", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFullPath(path), Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase))
            .Select(path => new
            {
                Path = path,
                WriteTime = File.GetLastWriteTimeUtc(path)
            })
            .Where(build => build.WriteTime > currentWriteTime)
            .OrderByDescending(build => build.WriteTime)
            .FirstOrDefault();

        if (newerBuild is null)
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = newerBuild.Path,
            WorkingDirectory = Path.GetDirectoryName(newerBuild.Path),
            UseShellExecute = true
        });

        return true;
    }

    private static string? FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Chess.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}