using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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

        Title = "Chess v1.0.0";
    }

    private static bool TryLaunchNewerVersion()
    {
        var currentPath = Environment.ProcessPath;
        var projectRoot = FindProjectRoot();
        if (currentPath is null || projectRoot is null)
        {
            return false;
        }

        var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
        var newerBuild = Directory.EnumerateFiles(Path.Combine(projectRoot, "bin"), "Chess.exe", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFullPath(path), Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase))
            .Select(path => new
            {
                Path = path,
                Version = GetFileVersion(path)
            })
            .Where(build => build.Version > currentVersion)
            .OrderByDescending(build => build.Version)
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

    private static Version GetFileVersion(string path)
    {
        var version = FileVersionInfo.GetVersionInfo(path).FileVersion;
        return Version.TryParse(version, out var parsedVersion) ? parsedVersion : new Version(0, 0);
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