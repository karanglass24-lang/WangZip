using System.Diagnostics;
using System.IO;
using System.Windows;
using WangZip.App.Dialogs;
using WangZip.Core;
using WangZip.Core.Zip;

namespace WangZip.App;

/// <summary>
/// 탐색기 우클릭 메뉴용 짧은 명령 모드.
///   WangZip.exe here-extract "파일.zip"     → 압축 파일 옆에 폴더를 만들어 풉니다.
///   WangZip.exe here-create  "폴더|파일"   → 같은 폴더에 "이름.zip"을 만듭니다.
/// 명령 실행에 성공하면 종료하고, 실패하면 안내 후 종료합니다 (창을 띄우지 않음).
/// </summary>
internal static class ShellCommands
{
    public static bool TryRun(string[] args)
    {
        if (args.Length < 2) return false;

        bool handled = args[0].ToLowerInvariant() switch
        {
            "here-extract" => RunExtract(args[1]),
            "here-create" => RunCreate(args[1]),
            _ => false,
        };

        if (handled)
        {
            Application.Current?.Shutdown();
        }

        return handled;
    }

    private static bool RunExtract(string archivePath)
    {
        try
        {
            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException("압축 파일을 찾을 수 없습니다.", archivePath);
            }

            string zip = Path.GetFullPath(archivePath);
            string destination = Path.Combine(
                Path.GetDirectoryName(zip)!,
                Path.GetFileNameWithoutExtension(zip));

            IArchiveSource source = ArchiveService.Open(zip);

            string? password = null;
            if (source.Entries.Any(entry => entry.IsEncrypted))
            {
                password = PromptPassword(zip);
                if (password is null)
                {
                    Show("취소했습니다.");
                    return true;
                }
            }

            var result = source.Extract(
                [],
                new ExtractionOptions
                {
                    DestinationDirectory = destination,
                    Overwrite = OverwritePolicy.Rename,
                    Password = password,
                },
                null,
                CancellationToken.None);

            Show($"압축을 풀었습니다: 파일 {result.ExtractedFiles:N0}개\n{destination}");
            return true;
        }
        catch (EncryptedArchiveException)
        {
            Show("비밀번호가 올바르지 않습니다. 다시 시도해 주세요.");
            return true;
        }
        catch (Exception exception)
        {
            Show("오류: " + exception.Message);
            return true;
        }
    }

    private static string? PromptPassword(string zipPath)
    {
        MessageBox.Show(
            "압축 파일에 비밀번호가 걸려 있습니다.\n비밀번호를 입력해 주세요.",
            "왕집", MessageBoxButton.OK, MessageBoxImage.Information);
        var dialog = new PasswordDialog();
        if (dialog.ShowDialog() == true)
        {
            return dialog.Password;
        }
        return null;
    }

    private static bool RunCreate(string sourcePath)
    {
        try
        {
            string full = Path.GetFullPath(sourcePath);
            string directory = Path.GetDirectoryName(full)!;

            if (!File.Exists(full) && !Directory.Exists(full))
            {
                throw new FileNotFoundException("파일이나 폴더를 찾을 수 없습니다.", full);
            }

            string name = Path.GetFileNameWithoutExtension(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string outputName = string.IsNullOrEmpty(name) ? "압축" : name;
            string output = Path.Combine(directory, outputName + ".zip");

            int attempt = 1;
            while (File.Exists(output) || Directory.Exists(output))
            {
                attempt++;
                if (attempt > 999) throw new IOException("저장 이름을 찾을 수 없습니다.");
                output = Path.Combine(directory, $"{outputName}-{attempt}.zip");
            }

            var created = ZipWriter.Create(output, [full], new CreationOptions(), null, CancellationToken.None);
            Show($"압축 파일을 만들었습니다:\n{created.OutputPath}\n({created.EntryCount:N0}개 항목)");
            return true;
        }
        catch (Exception exception)
        {
            Show("오류: " + exception.Message);
            return true;
        }
    }

    private static void Show(string message)
    {
        MessageBox.Show(message, "왕집", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
