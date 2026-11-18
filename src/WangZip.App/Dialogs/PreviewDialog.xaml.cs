using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WangZip.Core;

namespace WangZip.App.Dialogs;

public partial class PreviewDialog : Window
{
    private readonly string _tempFilePath;
    private readonly string _originalName;
    private Encoding _currentEncoding = Encoding.UTF8;

    public PreviewDialog(string tempFilePath, string originalName)
    {
        InitializeComponent();
        Localizer.Apply(this);

        _tempFilePath = tempFilePath;
        _originalName = originalName;
        FileNameText.Text = originalName;
        EncodingCombo.SelectedIndex = 0;

        Loaded += async (_, _) => await LoadTextAsync();
        TextBox.SelectionChanged += TextBox_SelectionChanged;
    }

    private async Task LoadTextAsync()
    {
        try
        {
            StatusText.Text = "로딩 중...";
            await Task.Run(() => DetectAndLoadEncoding());
        }
        catch (Exception ex)
        {
            TextBox.Text = $"파일을 읽는 중 오류가 발생했습니다:\n{ex.Message}";
            StatusText.Text = "오류";
        }
    }

    private void DetectAndLoadEncoding()
    {
        byte[] bytes = File.ReadAllBytes(_tempFilePath);

        // 자동 감지 로직
        Encoding detected = DetectEncoding(bytes);
        _currentEncoding = detected;

        // UI 스레드에서 콤보박스 업데이트
        Dispatcher.Invoke(() =>
        {
            foreach (ComboBoxItem item in EncodingCombo.Items)
            {
                if (item.Tag?.ToString() == GetEncodingTag(detected))
                {
                    EncodingCombo.SelectedItem = item;
                    break;
                }
            }
        });

        string text = detected.GetString(bytes);
        Dispatcher.Invoke(() =>
        {
            TextBox.Text = text;
            StatusText.Text = $"{FormatBytes(bytes.Length)} · {detected.EncodingName}";
            UpdateLineCol();
        });
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        // BOM 체크
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false); // UTF-8 BOM
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode; // UTF-16 LE BOM
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode; // UTF-16 BE BOM
        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            return Encoding.GetEncoding("utf-32"); // UTF-32 BE BOM
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            return Encoding.GetEncoding("utf-32"); // UTF-32 LE BOM

        // UTF-8 유효성 검사 (BOM 없음)
        if (IsValidUtf8(bytes))
            return new UTF8Encoding(false);

        // CP949(한글) 휴리스틱: 0x80~0xFF 범위 바이트가 많은 경우
        if (LikelyCp949(bytes))
            return Encoding.GetEncoding(949); // CP949

        // 기본값: UTF-8
        return new UTF8Encoding(false);
    }

    private static bool IsValidUtf8(byte[] bytes)
    {
        int i = 0;
        while (i < bytes.Length)
        {
            byte b = bytes[i];
            if (b < 0x80) { i++; continue; } // ASCII
            if ((b & 0xE0) == 0xC0) { if (i + 1 >= bytes.Length || (bytes[i + 1] & 0xC0) != 0x80) return false; i += 2; }
            else if ((b & 0xF0) == 0xE0) { if (i + 2 >= bytes.Length || (bytes[i + 1] & 0xC0) != 0x80 || (bytes[i + 2] & 0xC0) != 0x80) return false; i += 3; }
            else if ((b & 0xF8) == 0xF0) { if (i + 3 >= bytes.Length || (bytes[i + 1] & 0xC0) != 0x80 || (bytes[i + 2] & 0xC0) != 0x80 || (bytes[i + 3] & 0xC0) != 0x80) return false; i += 4; }
            else return false;
        }
        return true;
    }

    private static bool LikelyCp949(byte[] bytes)
    {
        int highByteCount = 0;
        int totalNonAscii = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] >= 0x80)
            {
                totalNonAscii++;
                // CP949 선행 바이트: 0x81~0xFE, 후행 바이트: 0x41~0x5A, 0x61~0x7A, 0x81~0xFE
                if (bytes[i] >= 0x81 && bytes[i] <= 0xFE)
                {
                    if (i + 1 < bytes.Length)
                    {
                        byte next = bytes[i + 1];
                        if ((next >= 0x41 && next <= 0x5A) || (next >= 0x61 && next <= 0x7A) || (next >= 0x81 && next <= 0xFE))
                        {
                            highByteCount++;
                            i++; // 후행 바이트 건너뛰기
                        }
                    }
                }
            }
        }
        return totalNonAscii > 0 && (double)highByteCount / totalNonAscii > 0.5;
    }

    private static string GetEncodingTag(Encoding enc)
    {
        return enc.CodePage switch
        {
            65001 => "utf-8",
            1200 => "utf-16",
            1201 => "utf-16", // BigEndianUnicode
            949 => "cp949",
            51949 => "euc-kr",
            28591 => "iso-8859-1",
            _ => "auto"
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        int i = 0;
        double size = bytes;
        while (size >= 1024 && i < units.Length - 1)
        {
            size /= 1024;
            i++;
        }
        return $"{size:0.##} {units[i]}";
    }

    private void EncodingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EncodingCombo.SelectedItem is not ComboBoxItem item) return;
        string tag = item.Tag?.ToString() ?? "auto";
        ReloadWithEncoding(tag);
    }

    private void ReloadWithEncoding(string tag)
    {
        try
        {
            Encoding enc = tag switch
            {
                "utf-8" => new UTF8Encoding(false),
                "utf-16" => Encoding.Unicode,
                "cp949" => Encoding.GetEncoding(949),
                "euc-kr" => Encoding.GetEncoding(51949),
                "iso-8859-1" => Encoding.GetEncoding(28591),
                _ => DetectEncoding(File.ReadAllBytes(_tempFilePath))
            };
            _currentEncoding = enc;
            byte[] bytes = File.ReadAllBytes(_tempFilePath);
            TextBox.Text = enc.GetString(bytes);
            StatusText.Text = $"{FormatBytes(bytes.Length)} · {enc.EncodingName}";
            UpdateLineCol();
        }
        catch (Exception ex)
        {
            TextBox.Text = $"인코딩 변경 중 오류:\n{ex.Message}";
            StatusText.Text = "오류";
        }
    }

    private void WordWrapCheck_Changed(object sender, RoutedEventArgs e)
    {
        TextBox.TextWrapping = WordWrapCheck.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    private void OpenExternalBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_tempFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"외부 프로그램으로 열기 실패:\n{ex.Message}", "왕집", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateLineCol();
    }

    private void UpdateLineCol()
    {
        int line = TextBox.GetLineIndexFromCharacterIndex(TextBox.CaretIndex) + 1;
        int col = TextBox.CaretIndex - TextBox.GetCharacterIndexFromLineIndex(line - 1) + 1;
        LineColText.Text = $"Ln {line}, Col {col}";
    }
}