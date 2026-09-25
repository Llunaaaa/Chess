using System;
using System.IO;
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
    private Button? activeSectionButton;
    private AppWindow? appWindow;
    private readonly Random random = new();

    public MainWindow()
    {
        InitializeComponent();

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "chessicon.ico"));
        RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);

        Title = "Chess v1.0.2-dev.5";
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

    private string UserDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Chess");
    private string AccountsPath => Path.Combine(UserDataDirectory, "ChessAccounts.json");
    private string CurrentAccountPath => Path.Combine(UserDataDirectory, "ChessCurrentAccount.txt");
    private string LegacyAccountsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ChessAccounts.json");
    private string LegacyCurrentAccountPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ChessCurrentAccount.txt");

    private void AccountButton_Click(object sender, RoutedEventArgs e)
    {
        AccountActions.Visibility = AccountActions.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        AccountList.Visibility = Visibility.Collapsed;
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

    private void Home_Click(object sender, RoutedEventArgs e) => ShowMenu(HomeContent);

    private void Puzzles_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent, PuzzlesButton);

    private void Learn_Click(object sender, RoutedEventArgs e) => ShowMenu(LearnContent, LearnButton);

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
        if (activeSectionButton == BotsButton || activeSectionButton == InPersonButton)
        {
            SetActiveNavigation(activeSectionButton);
        }
        else
        {
            activeSectionButton = PlayButton;
            SetActiveNavigation(PlayButton);
        }
    }

    private void Bots_Click(object sender, RoutedEventArgs e) => ShowMenu(BotsContent, BotsButton);

    private void InPerson_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent, InPersonButton);

    private void ShowMenu(UIElement selectedContent, Button? selectedButton = null)
    {
        if (selectedContent == HomeContent)
        {
            SetWelcomeMessage();
        }

        HomeContent.Visibility = selectedContent == HomeContent ? Visibility.Visible : Visibility.Collapsed;
        BotsContent.Visibility = selectedContent == BotsContent ? Visibility.Visible : Visibility.Collapsed;
        EngineContent.Visibility = selectedContent == BotsContent ? Visibility.Visible : Visibility.Collapsed;
        LearnContent.Visibility = selectedContent == LearnContent ? Visibility.Visible : Visibility.Collapsed;
        EmptyMenuContent.Visibility = selectedContent == EmptyMenuContent ? Visibility.Visible : Visibility.Collapsed;
        SetActiveNavigation(selectedButton ?? (selectedContent == HomeContent ? HomeButton : PlayButton));
    }

    private void SetWelcomeMessage()
    {
        var messagesPath = Path.Combine(AppContext.BaseDirectory, "WelcomeMessages.json");
        var messages = new List<string>();

        try
        {
            if (File.Exists(messagesPath))
            {
                messages = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(messagesPath)) ?? messages;
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        if (messages.Count == 0)
        {
            messages.Add("Welcome back, {username}");
        }

        var message = messages[random.Next(messages.Count)];
        WelcomeMessageText.Text = message.Replace("{username}", AccountNameText.Text, StringComparison.Ordinal);
    }

    private void SetActiveNavigation(Button selectedButton)
    {
        activeSectionButton = selectedButton;
        var inactiveBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        var activeBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 37, 32));
        HomeButton.Background = inactiveBrush;
        PlayButton.Background = inactiveBrush;
        BotsButton.Background = inactiveBrush;
        InPersonButton.Background = inactiveBrush;
        PuzzlesButton.Background = inactiveBrush;
        LearnButton.Background = inactiveBrush;
        SettingsButton.Background = inactiveBrush;
        selectedButton.Background = activeBrush;
        if (selectedButton == BotsButton || selectedButton == InPersonButton)
        {
            PlayButton.Background = activeBrush;
        }
    }

    private void CloseAccount_Click(object sender, RoutedEventArgs e)
    {
        if (AccountNameText.Text != "Guest")
        {
            AccountOverlay.Visibility = Visibility.Collapsed;
        }
    }

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
        LoginAccountList.Visibility = Visibility.Collapsed;
        SignUpTab.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
        LogInTab.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 54, 54, 54));
        AccountErrorText.Text = string.Empty;
    }

    private void ShowLoginAccountList_Click(object sender, RoutedEventArgs e)
    {
        LoginAccountList.Children.Clear();
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
            LoginAccountList.Children.Add(accountButton);
        }

        LoginAccountList.Visibility = LoginAccountList.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
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
        AccountTabs.Visibility = Visibility.Visible;
        ShowLogIn_Click(this, new RoutedEventArgs());
    }

    private bool RestoreLoggedInAccount()
    {
        try
        {
            MigrateLegacyAccountData();
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
            Directory.CreateDirectory(UserDataDirectory);
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
            Directory.CreateDirectory(UserDataDirectory);
            if (File.Exists(AccountsPath))
            {
                var accounts = JsonSerializer.Deserialize<List<LocalAccount>>(File.ReadAllText(AccountsPath)) ?? new List<LocalAccount>();
                foreach (var account in accounts)
                {
                    account.Data ??= new AccountData();
                    account.Data.Progression ??= new Dictionary<string, string>();
                    account.Data.GameLog ??= new List<string>();
                    account.Data.Preferences ??= new Dictionary<string, string>();
                }
                return accounts;
            }
        }
        catch (JsonException)
        {
        }

        return new List<LocalAccount>();
    }

    private void SaveAccounts(List<LocalAccount> accounts)
    {
        Directory.CreateDirectory(UserDataDirectory);
        var json = JsonSerializer.Serialize(accounts, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AccountsPath, json);
    }

    private void MigrateLegacyAccountData()
    {
        Directory.CreateDirectory(UserDataDirectory);

        if (!File.Exists(AccountsPath) && File.Exists(LegacyAccountsPath))
        {
            File.Copy(LegacyAccountsPath, AccountsPath);
        }

        if (!File.Exists(CurrentAccountPath) && File.Exists(LegacyCurrentAccountPath))
        {
            File.Copy(LegacyCurrentAccountPath, CurrentAccountPath);
        }
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
        public AccountData Data { get; set; } = new();
    }

    private sealed class AccountData
    {
        public Dictionary<string, string> Progression { get; set; } = new();
        public List<string> GameLog { get; set; } = new();
        public Dictionary<string, string> Preferences { get; set; } = new();
    }

}