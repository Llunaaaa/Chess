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
using WinRT.Interop;

namespace Chess;

public sealed partial class MainWindow : Window
{
    public bool RedirectedToNewerVersion { get; }

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
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "chessicon.ico"));

        Title = "Chess v1.0.1-preview.1";
        RestoreLoggedInAccount();
    }

    private string AccountsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ChessAccounts.json");
    private string CurrentAccountPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ChessCurrentAccount.txt");

    private void GuestAccount_Click(object sender, RoutedEventArgs e)
    {
        AccountOverlay.Visibility = Visibility.Visible;
        ShowSignUp_Click(sender, e);
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        PlaySubmenu.Visibility = PlaySubmenu.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Online_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent);

    private void Bots_Click(object sender, RoutedEventArgs e) => ShowMenu(BotsContent);

    private void InPerson_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent);

    private void Special_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent);

    private void ShowMenu(UIElement selectedContent)
    {
        BotsContent.Visibility = selectedContent == BotsContent ? Visibility.Visible : Visibility.Collapsed;
        EngineContent.Visibility = selectedContent == BotsContent ? Visibility.Visible : Visibility.Collapsed;
        EmptyMenuContent.Visibility = selectedContent == EmptyMenuContent ? Visibility.Visible : Visibility.Collapsed;
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
        SaveCurrentAccount(username);
    }

    private void RestoreLoggedInAccount()
    {
        try
        {
            if (!File.Exists(CurrentAccountPath))
            {
                return;
            }

            var username = File.ReadAllText(CurrentAccountPath).Trim();
            var account = LoadAccounts().Find(candidate => string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase));
            if (account is not null)
            {
                LogInAccount(account.Username);
            }
        }
        catch (IOException)
        {
        }
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