using System.Windows;
using System.Windows.Controls;
using WangZip.App.ViewModels;
using WangZip.Core;

namespace WangZip.App.Dialogs;

public partial class PreferencesDialog : Window
{
    private readonly AppSettings _settings;
    private readonly List<CheckBox> _extensionBoxes = [];

    // 창을 열 때의 탐색기 연동 상태. 바뀐 경우에만 레지스트리에 씁니다.
    private readonly bool _initialShellMenu;
    private readonly HashSet<string> _initialAssociations = new(StringComparer.OrdinalIgnoreCase);

    public PreferencesDialog(AppSettings settings)
    {
        InitializeComponent();
        Localizer.Apply(this);
        _settings = settings;

        FillCombos();
        LoadFrom(settings);

        _initialShellMenu = ShellIntegration.IsShellMenuEnabled();
        ShellMenuBox.IsChecked = _initialShellMenu;

        foreach (string extension in ShellIntegration.AssociationExtensions)
        {
            bool associated = ShellIntegration.IsAssociated(extension);
            if (associated) _initialAssociations.Add(extension);

            var box = new CheckBox
            {
                Content = extension,
                Tag = extension,
                IsChecked = associated,
                Width = 90,
                Margin = new Thickness(0, 0, 0, 6),
            };
            _extensionBoxes.Add(box);
            ExtensionPanel.Children.Add(box);
        }
    }

    /// <summary>목록형 선택 상자를 채웁니다. 순서는 각 대화상자의 목록과 같아야 저장된 번호가 맞습니다.</summary>
    private void FillCombos()
    {
        QuickDestBox.ItemsSource = MainViewModel.QuickDestinations
            .Select(destination => destination.Label)
            .Concat([Loc.T("직접 입력 (경로 지정)")])
            .ToList();

        SubfolderBox.ItemsSource = new[]
        {
            Loc.T("만들지 않음 (선택한 위치에 바로 풀기)"),
            Loc.T("자동 (최상위 폴더가 여러 개일 때만 만들기)"),
            Loc.T("항상 만들기"),
        };

        OverwriteBox.ItemsSource = new[]
        {
            Loc.T("덮어쓰기"),
            Loc.T("건너뛰기"),
            Loc.T("이름 바꾸기"),
        };

        LevelBox.ItemsSource = CreateDialog.LevelOptions().Select(option => option.Label).ToList();
        NameEncodingBox.ItemsSource = CreateDialog.EncodingOptions().Select(option => option.Label).ToList();
        SplitBox.ItemsSource = CreateDialog.SplitOptions().Select(option => option.Label).ToList();
    }

    private static void Select(ComboBox box, int index) =>
        box.SelectedIndex = Math.Clamp(index, 0, Math.Max(0, box.Items.Count - 1));

    /// <summary>탐색기 연동을 뺀 모든 항목을 설정 값으로 채웁니다 ("기본값으로 되돌리기"에도 씁니다).</summary>
    private void LoadFrom(AppSettings settings)
    {
        LanguageBox.SelectedItem = null;
        foreach (ComboBoxItem item in LanguageBox.Items)
        {
            if ((string)item.Tag == settings.Language) LanguageBox.SelectedItem = item;
        }

        LanguageBox.SelectedItem ??= LanguageBox.Items[0];

        Select(ThemeBox, settings.Theme);
        AutoUpdateBox.IsChecked = settings.AutoUpdate;

        Select(QuickDestBox, settings.QuickDestination);
        Select(SubfolderBox, settings.SubfolderMode);
        Select(OverwriteBox, settings.DefaultOverwrite);
        AutoOpenBox.IsChecked = settings.AutoOpenAfterExtract;
        DeleteSourceBox.IsChecked = settings.DeleteSourceAfterExtract;
        ShowCompletionBox.IsChecked = settings.ShowCompletionDialog;
        ShellUsesBox.IsChecked = settings.ExtractShellUsesAppSettings;

        Select(LevelBox, settings.DefaultCompressionLevel);
        Select(NameEncodingBox, settings.DefaultNameEncoding);
        Select(SplitBox, settings.DefaultSplitSizeIndex);

        Select(ListEncodingBox, settings.DefaultListEncoding);
        ConfirmDeleteBox.IsChecked = settings.ConfirmBeforeDelete;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) =>
        _extensionBoxes.ForEach(box => box.IsChecked = true);

    private void SelectNone_Click(object sender, RoutedEventArgs e) =>
        _extensionBoxes.ForEach(box => box.IsChecked = false);

    private void OpenDefaultApps_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellIntegration.OpenDefaultAppsSettings();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClearRecent_Click(object sender, RoutedEventArgs e)
    {
        // 창 안의 다른 값과 달리 저장 단추와 상관없이 바로 적용됩니다.
        _settings.RecentPaths = [];
        _settings.Save();
        MessageBox.Show(this, Loc.T("최근 목록을 지웠습니다."), Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            Loc.T("이 창의 설정(일반·압축 풀기·압축하기·목록)을 기본값으로 되돌릴까요?\n저장을 눌러야 적용됩니다. 탐색기 연동·창 위치·최근 목록은 바뀌지 않습니다."),
            Loc.T("왕집"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        LoadFrom(new AppSettings());
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static int Index(ComboBox box) => Math.Max(0, box.SelectedIndex);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 일반 설정은 탐색기 연동(레지스트리) 성공 여부와 상관없이 항상 저장합니다.
            string chosenLanguage = (string)((ComboBoxItem)LanguageBox.SelectedItem).Tag;
            bool languageChanged = chosenLanguage != _settings.Language;
            int theme = Index(ThemeBox);
            bool themeChanged = theme != _settings.Theme;

            _settings.Language = chosenLanguage;
            _settings.Theme = theme;
            _settings.AutoUpdate = AutoUpdateBox.IsChecked == true;

            _settings.QuickDestination = Index(QuickDestBox);
            _settings.SubfolderMode = Index(SubfolderBox);
            _settings.DefaultOverwrite = Index(OverwriteBox);
            _settings.AutoOpenAfterExtract = AutoOpenBox.IsChecked == true;
            _settings.DeleteSourceAfterExtract = DeleteSourceBox.IsChecked == true;
            _settings.ShowCompletionDialog = ShowCompletionBox.IsChecked == true;
            _settings.ExtractShellUsesAppSettings = ShellUsesBox.IsChecked == true;

            _settings.DefaultCompressionLevel = Index(LevelBox);
            _settings.DefaultNameEncoding = Index(NameEncodingBox);
            _settings.DefaultSplitSizeIndex = Index(SplitBox);

            _settings.DefaultListEncoding = Index(ListEncodingBox);
            _settings.ConfirmBeforeDelete = ConfirmDeleteBox.IsChecked == true;

            // 탐색기 연동은 값이 실제로 바뀐 경우에만 레지스트리에 씁니다.
            string? shellError = null;
            bool shellMenu = ShellMenuBox.IsChecked == true;
            List<string> chosenExtensions = _extensionBoxes
                .Where(box => box.IsChecked == true)
                .Select(box => (string)box.Tag)
                .ToList();
            bool menuChanged = shellMenu != _initialShellMenu;
            bool associationsChanged = !_initialAssociations.SetEquals(chosenExtensions);

            if (menuChanged || associationsChanged)
            {
                try
                {
                    string exe = Environment.ProcessPath
                        ?? throw new InvalidOperationException(Loc.T("실행 파일 경로를 알 수 없습니다."));
                    if (string.Equals(System.IO.Path.GetFileName(exe), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(Loc.T("개발 중 실행(dotnet) 상태에서는 탐색기 등록을 변경할 수 없습니다."));
                    }

                    if (menuChanged)
                    {
                        ShellIntegration.SetShellMenu(shellMenu, exe);
                        _settings.ShellMenuEnabled = shellMenu;
                    }

                    if (associationsChanged)
                    {
                        ShellIntegration.SetAssociations(chosenExtensions, exe);
                    }
                }
                catch (Exception exception)
                {
                    shellError = exception.Message;
                }
            }

            _settings.Save();
            if (themeChanged) App.ApplyTheme(theme);

            if (languageChanged)
            {
                MessageBox.Show(this, Loc.T("언어는 왕집을 다시 시작하면 적용됩니다."), Loc.T("왕집"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            if (shellError is not null)
            {
                MessageBox.Show(this,
                    Loc.T("탐색기 연동 설정을 적용하지 못했습니다. 다른 설정은 저장했습니다.\n") + shellError,
                    Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            DialogResult = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, Loc.T("설정을 저장하지 못했습니다.\n") + exception.Message,
                Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
