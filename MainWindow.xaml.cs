using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chess.Models;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;
using Windows.System;

namespace Chess;

public sealed partial class MainWindow : Window
{
    private Button? activeSectionButton;
    private AppWindow? appWindow;
    private readonly Random random = new();
    private UIElement? activeContent;
    private ScrollViewer? activeScrollViewer;
    private int scrollDirection;
    private double scrollVelocity;
    private readonly ChessBoard foundationsBoard = ChessBoard.CreateInitialPosition();
    private readonly Dictionary<BoardSquare, Image> foundationsPieceImages = new();
    private FoundationsLessonStep foundationsStep = FoundationsLessonStep.Welcome;

    public MainWindow()
    {
        InitializeComponent();

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "chessicon.ico"));
        RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);
        RootGrid.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler(RootGrid_KeyUp), true);
        CompositionTarget.Rendering += OnFrame;
        foundationsBoard.PromotionRequested += FoundationsBoard_PromotionRequested;
        GenerateFoundationsBoard();

        Title = "Chess v1.0.2-dev.7";
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
            return;
        }

        if (args.Key == VirtualKey.Up)
        {
            scrollDirection = -1;
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Down)
        {
            scrollDirection = 1;
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Left)
        {
            NavigateBack();
            args.Handled = true;
        }
    }

    private void RootGrid_KeyUp(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Up or VirtualKey.Down)
        {
            scrollDirection = 0;
            args.Handled = true;
        }
    }

    private void OnFrame(object? sender, object args)
    {
        if (activeScrollViewer is null || activeScrollViewer.ScrollableHeight <= 0)
        {
            scrollVelocity = 0;
            return;
        }

        var targetVelocity = scrollDirection * Math.Max(0.35, activeScrollViewer.ViewportHeight * 0.006);
        if (scrollDirection != 0 && Math.Sign(scrollVelocity) != 0 && Math.Sign(scrollVelocity) != scrollDirection)
        {
            scrollVelocity = targetVelocity * 0.35;
        }
        else
        {
            scrollVelocity += (targetVelocity - scrollVelocity) * 0.2;
        }
        if (scrollDirection == 0)
        {
            scrollVelocity *= 0.82;
        }

        if (Math.Abs(scrollVelocity) < 0.05)
        {
            scrollVelocity = 0;
            return;
        }

        var offset = Math.Clamp(activeScrollViewer.VerticalOffset + scrollVelocity, 0, activeScrollViewer.ScrollableHeight);
        activeScrollViewer.ChangeView(null, offset, null, true);
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

    private void Foundations_Click(object sender, RoutedEventArgs e)
    {
        InitializeFoundationsLesson();
        ShowMenu(FoundationsLessonContent, LearnButton);
    }

    private void BlankLearnOption_Click(object sender, RoutedEventArgs e) => ShowMenu(EmptyMenuContent, LearnButton);

    private void OpeningLibrary_Click(object sender, RoutedEventArgs e)
    {
        BuildOpeningCategories();
        ShowMenu(OpeningLibraryContent, LearnButton);
    }

    private void BackToLearn_Click(object sender, RoutedEventArgs e) => ShowMenu(LearnContent, LearnButton);

    private void BackToOpeningLibrary_Click(object sender, RoutedEventArgs e) => OpeningLibrary_Click(sender, e);

    private void BackToOpeningList_Click(object sender, RoutedEventArgs e) => ShowMenu(OpeningListContent, LearnButton);

    private void NavigateBack()
    {
        if (activeContent == OpeningLessonContent)
        {
            BackToOpeningList_Click(this, new RoutedEventArgs());
        }
        else if (activeContent == OpeningListContent)
        {
            BackToOpeningLibrary_Click(this, new RoutedEventArgs());
        }
        else if (activeContent == OpeningLibraryContent)
        {
            BackToLearn_Click(this, new RoutedEventArgs());
        }
        else if (activeContent == FoundationsLessonContent)
        {
            BackToLearn_Click(this, new RoutedEventArgs());
        }
    }

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
        activeContent = selectedContent;
        activeScrollViewer = selectedContent as ScrollViewer;
        if (selectedContent == HomeContent)
        {
            SetWelcomeMessage();
        }
        else if (selectedContent == LearnContent)
        {
            UpdateLearningDashboard();
        }

        HomeContent.Visibility = selectedContent == HomeContent ? Visibility.Visible : Visibility.Collapsed;
        BotsContent.Visibility = selectedContent == BotsContent ? Visibility.Visible : Visibility.Collapsed;
        EngineContent.Visibility = selectedContent == BotsContent ? Visibility.Visible : Visibility.Collapsed;
        LearnContent.Visibility = selectedContent == LearnContent ? Visibility.Visible : Visibility.Collapsed;
        OpeningLibraryContent.Visibility = selectedContent == OpeningLibraryContent ? Visibility.Visible : Visibility.Collapsed;
        OpeningListContent.Visibility = selectedContent == OpeningListContent ? Visibility.Visible : Visibility.Collapsed;
        OpeningLessonContent.Visibility = selectedContent == OpeningLessonContent ? Visibility.Visible : Visibility.Collapsed;
        FoundationsLessonContent.Visibility = selectedContent == FoundationsLessonContent ? Visibility.Visible : Visibility.Collapsed;
        EmptyMenuContent.Visibility = selectedContent == EmptyMenuContent ? Visibility.Visible : Visibility.Collapsed;
        SetActiveNavigation(selectedButton ?? (selectedContent == HomeContent ? HomeButton : PlayButton));
    }

    private void InitializeFoundationsLesson()
    {
        var account = GetCurrentAccount();
        foundationsStep = account is null
            ? FoundationsLessonStep.Welcome
            : LearningProgress.GetFoundationsStep(account.Data.Progression);

        foundationsBoard.ClearPosition();
        FoundationsLessonStepText.Text = foundationsStep == FoundationsLessonStep.Welcome
            ? "The basics  •  Lesson 1 of 5"
            : "The basics  •  Lesson 1 of 5  •  In progress";
        FoundationsNextButton.Content = "Next";
        FoundationsNextButton.IsEnabled = foundationsStep != FoundationsLessonStep.IncrementVisible;
        FoundationsPromotionPanel.Visibility = Visibility.Collapsed;
        if (foundationsStep == FoundationsLessonStep.IncrementVisible)
        {
            ShowFoundationsIncrementLesson();
        }
        else if (foundationsStep == FoundationsLessonStep.TimeVisible)
        {
            ShowFoundationsTimeLesson();
        }
        else
        {
            ShowFoundationsIntroduction();
        }
        RenderFoundationsBoard();
    }

    private void FoundationsNext_Click(object sender, RoutedEventArgs e)
    {
        if (foundationsStep == FoundationsLessonStep.IncrementVisible)
        {
            return;
        }

        foundationsStep = foundationsStep == FoundationsLessonStep.TimeVisible
            ? FoundationsLessonStep.IncrementVisible
            : FoundationsLessonStep.TimeVisible;
        foundationsBoard.ClearPosition();
        FoundationsLessonStepText.Text = "The basics  •  Lesson 1 of 5  •  In progress";
        FoundationsNextButton.IsEnabled = foundationsStep != FoundationsLessonStep.IncrementVisible;
        if (foundationsStep == FoundationsLessonStep.IncrementVisible)
        {
            ShowFoundationsIncrementLesson();
        }
        else
        {
            ShowFoundationsTimeLesson();
        }
        RenderFoundationsBoard();

        var accounts = LoadAccounts();
        var account = accounts.Find(candidate => string.Equals(candidate.Username, AccountNameText.Text, StringComparison.OrdinalIgnoreCase));
        if (account is not null)
        {
            account.Data.Progression[LearningProgressKeys.FoundationsStep] = foundationsStep.ToString();
            account.Data.Progression[LearningProgressKeys.FoundationsStarted] = "true";
            account.Data.Progression[LearningProgressKeys.FoundationsCourse] = "in_progress";
            SaveAccounts(accounts);
        }

        UpdateLearningDashboard();
    }

    private void ShowFoundationsIntroduction()
    {
        FoundationsLessonHeading.Text = "What is chess?";
        FoundationsLessonMessage.Text = "Chess is a two-player strategy game played on an 8×8 board. Each side commands sixteen pieces, and every move changes the position.";
        FoundationsLessonDetailLabel.Text = "THE OBJECTIVE";
        FoundationsLessonDetail.Text = "Checkmate the opposing king: place it under attack with no legal move left.";
        FoundationsLessonHint.Text = "Learn how each piece moves, then use them together to create threats and protect your king.";
    }

    private void ShowFoundationsTimeLesson()
    {
        FoundationsLessonHeading.Text = "Time is part of the game";
        FoundationsLessonMessage.Text = "In most chess games, each side gets an agreed amount of time for the whole game.";
        FoundationsLessonDetailLabel.Text = "HOW THE CLOCK WORKS";
        FoundationsLessonDetail.Text = "When it is your turn, your clock counts down. After you move, your clock pauses and your opponent's clock starts.";
        FoundationsLessonHint.Text = "Good chess decisions balance the position on the board with the time left on the clock.";
    }

    private void ShowFoundationsIncrementLesson()
    {
        FoundationsLessonHeading.Text = "Time can be added as you play";
        FoundationsLessonMessage.Text = "Some time controls give you a small bonus whenever you make a move, helping intense low-time games stay playable.";
        FoundationsLessonDetailLabel.Text = "EXAMPLE: 10+2 Rapid";
        FoundationsLessonDetail.Text = "Each player starts with 10 minutes. After every move, 2 seconds are added to the clock of the player who moved.";
        FoundationsLessonHint.Text = "The first number is the starting time (in minutes), while the second number is the bonus time (in seconds).";
    }

    private void GenerateFoundationsBoard()
    {
        FoundationsBoardGrid.Children.Clear();
        FoundationsBoardGrid.RowDefinitions.Clear();
        FoundationsBoardGrid.ColumnDefinitions.Clear();
        foundationsPieceImages.Clear();
        for (var index = 0; index < 8; index++)
        {
            FoundationsBoardGrid.RowDefinitions.Add(new RowDefinition());
            FoundationsBoardGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var row = 0; row < 8; row++)
        {
            for (var column = 0; column < 8; column++)
            {
                var square = new Grid
                {
                    Background = new SolidColorBrush((row + column) % 2 == 0
                        ? Windows.UI.Color.FromArgb(255, 217, 195, 157)
                        : Windows.UI.Color.FromArgb(255, 135, 103, 70))
                };
                var image = new Image
                {
                    Margin = new Thickness(8),
                    Stretch = Stretch.Uniform,
                    Visibility = Visibility.Collapsed
                };
                square.Children.Add(image);
                foundationsPieceImages[new BoardSquare(row, column)] = image;

                if (row == 7)
                {
                    square.Children.Add(new TextBlock
                    {
                        Text = ((char)('a' + column)).ToString(),
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 35, 29, 22)),
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 5, 3)
                    });
                }

                if (column == 0)
                {
                    square.Children.Add(new TextBlock
                    {
                        Text = (8 - row).ToString(),
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 35, 29, 22)),
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(5, 3, 0, 0)
                    });
                }

                Grid.SetRow(square, row);
                Grid.SetColumn(square, column);
                FoundationsBoardGrid.Children.Add(square);
            }
        }
    }

    private void RenderFoundationsBoard()
    {
        for (var row = 0; row < 8; row++)
        {
            for (var column = 0; column < 8; column++)
            {
                var image = foundationsPieceImages[new BoardSquare(row, column)];
                var piece = foundationsBoard[row, column];
                image.Source = piece is not null
                    ? CreatePieceImageSource(piece)
                    : null;
                image.Visibility = piece is not null ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private static SvgImageSource CreatePieceImageSource(ChessPiece piece)
    {
        return new SvgImageSource
        {
            UriSource = new Uri(ChessBoard.GetPieceAssetUri(piece)),
            RasterizePixelWidth = 96,
            RasterizePixelHeight = 96
        };
    }

    private void FoundationsBoard_PromotionRequested(object? sender, PromotionRequestedEventArgs e)
    {
        FoundationsPromotionPanel.Visibility = Visibility.Visible;
    }

    private void PromotionChoice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string pieceName } || !Enum.TryParse<PieceType>(pieceName, out var pieceType))
        {
            return;
        }

        if (foundationsBoard.ChoosePromotion(pieceType))
        {
            FoundationsPromotionPanel.Visibility = Visibility.Collapsed;
            RenderFoundationsBoard();
        }
    }

    private void UpdateLearningDashboard()
    {
        var progression = GetCurrentAccount()?.Data.Progression;
        var step = progression is null ? FoundationsLessonStep.Welcome : LearningProgress.GetFoundationsStep(progression);
        var started = step != FoundationsLessonStep.Welcome;
        FoundationsResumeTitle.Text = started ? "The basics" : "Start with Foundations";
        FoundationsResumeDetail.Text = "Welcome to Chess  •  Lesson 1 of 5";
        FoundationsResumeAction.Content = started ? "Continue lesson" : "Start lesson";
        FoundationsProgressText.Text = started ? "In progress  •  1 of 5 lessons" : "Not started  •  0 of 5 lessons";
        FoundationsJourneyStatus.Text = started ? "In progress  •  1 of 5 lessons" : "Not started  •  0 of 5 lessons";
        FoundationsJourneyMarker.Background = new SolidColorBrush(started
            ? Windows.UI.Color.FromArgb(255, 134, 196, 64)
            : Windows.UI.Color.FromArgb(255, 48, 54, 48));
    }

    private void BuildOpeningCategories()
    {
        OpeningCategoryPanel.Children.Clear();
        foreach (var category in OpeningCategories)
        {
            var button = new Button
            {
                Content = category.Name,
                Tag = category,
                Style = (Style)Application.Current.Resources["NavButtonStyle"],
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(16, 14, 16, 14),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            button.Click += OpeningCategory_Click;
            OpeningCategoryPanel.Children.Add(button);
        }
    }

    private void OpeningCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: OpeningCategory category })
        {
            return;
        }

        OpeningListTitle.Text = category.Name;
        OpeningListPanel.Children.Clear();
        foreach (var opening in category.Openings)
        {
            var button = new Button
            {
                Content = opening,
                Tag = category,
                Style = (Style)Application.Current.Resources["NavButtonStyle"],
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(16, 12, 16, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            button.Click += Opening_Click;
            OpeningListPanel.Children.Add(button);
        }

        ShowMenu(OpeningListContent, LearnButton);
    }

    private void Opening_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Content is not string opening || button.Tag is not OpeningCategory category)
        {
            return;
        }

        OpeningLessonTitle.Text = opening;
        OpeningLessonCategory.Text = category.Name;
        ShowMenu(OpeningLessonContent, LearnButton);
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
                var migrated = false;
                foreach (var account in accounts)
                {
                    migrated |= EnsureLearningProgress(account);
                }
                if (migrated)
                {
                    SaveAccounts(accounts);
                }
                return accounts;
            }
        }
        catch (JsonException)
        {
        }

        return new List<LocalAccount>();
    }

    private LocalAccount? GetCurrentAccount()
    {
        if (string.Equals(AccountNameText.Text, "Guest", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return LoadAccounts().Find(account => string.Equals(account.Username, AccountNameText.Text, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EnsureLearningProgress(LocalAccount account)
    {
        var progression = account.Data.Progression;
        if (progression.TryGetValue(LearningProgressKeys.SchemaVersion, out var schemaVersion)
            && schemaVersion == LearningProgressKeys.CurrentSchemaVersion)
        {
            return false;
        }

        progression[LearningProgressKeys.SchemaVersion] = LearningProgressKeys.CurrentSchemaVersion;
        progression[LearningProgressKeys.FoundationsStep] = FoundationsLessonStep.Welcome.ToString();
        progression[LearningProgressKeys.FoundationsStarted] = "false";
        progression[LearningProgressKeys.FoundationsCompletedLessons] = "0";
        progression[LearningProgressKeys.CurrentFoundationsLesson] = LearningProgressKeys.FoundationsLessonId;
        progression[LearningProgressKeys.FoundationsCourse] = "not_started";
        return true;
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

    private static readonly OpeningCategory[] OpeningCategories =
    {
        new("Flank & Irregular Openings", "English Opening", "Réti Opening", "King's Indian Attack", "Larsen's Opening", "Bird's Opening", "Sokolsky Opening (Polish Opening)", "Grob's Attack", "Dunst Opening", "Van't Kruijs Opening", "Mieses Opening", "Saragossa Opening", "Clemenz Opening", "Barnes Opening", "Ware Opening", "Desprez Opening", "Kadas Opening", "Amar Opening", "Amsterdam Opening"),
        new("Semi-Open Games", "Sicilian Defense", "Caro-Kann Defense", "French Defense", "Scandinavian Defense (Center Counter)", "Pirc Defense", "Modern Defense", "Alekhine's Defense", "Nimzowitsch Defense", "Owen's Defense", "Hippopotamus Defence", "St. George Defense", "Borg Defense", "Colorado Counter", "Lemming Defense"),
        new("Open Games", "Italian Game (Giuoco Piano)", "Ruy Lopez (Spanish Game)", "Petrov's Defense (Russian Game)", "Scotch Game", "Two Knights Defense", "Four Knights Game", "Vienna Game", "Philidor Defense", "King's Gambit", "Bishop's Opening", "Center Game", "Ponziani Opening", "Alapin's Opening", "Hungarian Defense", "Portuguese Opening", "Inverted Hungarian", "Konstantinopolsky Opening", "MacLeod Attack"),
        new("Closed & Semi-Closed Games", "Queen's Gambit", "London System", "King's Indian Defense", "Queen's Gambit Declined", "Slav Defense", "Nimzo-Indian Defense", "Grünfeld Defense", "Semi-Slav Defense", "Catalan Opening", "Dutch Defense", "Queen's Indian Defense", "Queen's Gambit Accepted", "Benoni Defense", "Trompowsky Attack", "Colle System", "Tarrasch Defense", "Bogo-Indian Defense", "Torre Attack", "Stonewall Attack", "Chigorin Defense", "Richter-Veresov Attack", "Old Indian Defense", "Neo-Grünfeld Defense", "Black Knights' Tango", "Polish Defense", "Keres Defense", "Lundin Defense", "Döry Defense"),
        new("Famous Gambits", "King's Gambit Accepted", "Evans Gambit", "Smith-Morra Gambit", "King's Gambit Declined", "Benko Gambit", "Englund Gambit", "Danish Gambit", "Budapest Gambit", "Stafford Gambit", "Albin Countergambit", "Vienna Gambit", "Blackmar-Diemer Gambit", "Tennison Gambit", "Falkbeer Countergambit", "Kieseritzky Gambit", "Urusov Gambit", "Latvian Gambit", "Elephant Gambit", "Halloween Gambit", "Göring Gambit", "Blumenfeld Gambit", "Rousseau Gambit", "Muzio Gambit", "Cochrane Gambit", "Colorado Gambit", "Jerome Gambit"),
        new("Major Variations & Sub-Systems", "Najdorf Variation", "Morphy Defense", "Berlin Defense", "Advance Variation", "Exchange Variation", "Classical Variation", "Dragon Variation", "Sveshnikov Variation", "Fried Liver Attack", "Giuoco Pianissimo", "Marshall Attack", "Taimanov Variation", "Kan Variation", "Accelerated Dragon", "Alapin Sicilian", "Grand Prix Attack", "Bowdler Attack", "Winawer Variation", "Scheveningen Variation", "Rubinstein Variation", "Meran Variation", "Moscow Variation", "Yugoslav Attack", "Traxler Counterattack", "Steinitz Defense", "Botvinnik System", "Sämisch Variation", "Four Pawns Attack", "Panov-Botvinnik Attack", "Max Lange Attack", "Schliemann Defense", "Cambridge Springs Defense", "Breyer Variation", "Zaitsev Variation", "Karpov Variation", "Botvinnik Variation", "Tartakower Defense", "Lasker Defense", "Orthodox Defense", "Fianchetto Variation", "Averbakh Variation", "Cunningham Defense", "Levenfish Attack", "Nottingham Variation")
    };

    private sealed class OpeningCategory
    {
        public OpeningCategory(string name, params string[] openings)
        {
            Name = name;
            Openings = openings;
        }

        public string Name { get; }
        public IReadOnlyList<string> Openings { get; }
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