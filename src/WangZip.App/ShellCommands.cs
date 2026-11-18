using System.IO;
using System.Windows;
using WangZip.App.Dialogs;
using WangZip.Core;
using WangZip.Core.Zip;

namespace WangZip.App;

/// <summary>
/// 탐색기 우클릭 메뉴용 짧은 명령 모드.
///   WangZip.exe here-extract      "파일.zip" → 압축 파일 옆에 폴더를 만들어 풉니다.
///   WangZip.exe here-extract-all  "파일.zip" → 선택한 압축 파일을 모두 풉니다 (경로 수집).
///   WangZip.exe here-create       "폴더|파일" → 같은 폴더에 "이름.zip"을 만듭니다.
/// 진행 창을 보여 주고, 끝나면 결과를 알린 뒤 종료합니다 (메인 창은 띄우지 않음).
/// </summary>
internal static class ShellCommands
{
    public static bool TryRun(string[] args)
    {
        if (args.Length < 2) return false;

        // 대화상자가 닫혀도 작업이 끝나기 전에는 종료되지 않도록 합니다.
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        switch (args[0].ToLowerInvariant())
        {
            case "here-extract":
                _ = RunAsync(() => RunExtractAsync(args[1]));
                return true;
            case "here-extract-all":
                _ = RunAsync(() => CollectAndExtractAllAsync(args[1]));
                return true;
            case "here-create":
                _ = RunAsync(() => CollectAndCreateAsync(args[1]));
                return true;
            default:
                return false;
        }
    }

    private static async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception exception)
        {
            Show(Loc.T("오류: ") + exception.Message);
        }
        finally
        {
            Application.Current?.Shutdown();
        }
    }

    private static async Task RunExtractAsync(string archivePath)
    {
        ArchiveExtraction? extraction = null;
        bool ok = await ProgressDialog.RunAsync(null!, Loc.T("압축을 푸는 중입니다"), async (progress, token) =>
        {
            extraction = await ExtractOneAsync(archivePath, progress, token);
        });

        if (ok && extraction is { } done)
        {
            Show(Loc.F("압축을 풀었습니다: 파일 {0:N0}개\n{1}", done.Result.ExtractedFiles, done.Destination));
        }
    }

    private sealed record ArchiveExtraction(ExtractionResult Result, string Destination);

    /// <summary>한 압축 파일의 전체 항목을 풉니다. 암호가 필요하면 사용자에게 묻습니다.</summary>
    private static async Task<ArchiveExtraction?> ExtractOneAsync(
        string archivePath, IProgress<ProgressReport> progress, CancellationToken token, bool quiet = false)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException(Loc.T("압축 파일을 찾을 수 없습니다."), archivePath);
        }

        string zip = Path.GetFullPath(archivePath);

        // 파일 이름까지 암호화된 압축 파일은 열 때부터 비밀번호가 필요합니다.
        IArchiveSource? source = null;
        string? password = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                source = ArchiveService.Open(zip, NameEncodingMode.Auto, password);
                break;
            }
            catch (EncryptedArchiveException)
            {
                password = PromptPassword(attempt > 0);
                if (password is null) return null;
            }
        }

        if (source is null)
        {
            if (!quiet) Show(Loc.T("비밀번호가 올바르지 않습니다."));
            return null;
        }

        using (source)
        {
            if (source.Entries.Count == 0)
            {
                if (!quiet) Show(Loc.T("압축 파일 안에 항목이 없습니다."));
                return null;
            }

            if (password is null && source.Entries.Any(entry => entry.IsEncrypted))
            {
                password = PromptPassword(false);
                if (password is null) return null;
            }

            string parent = Path.GetDirectoryName(zip)!;
            string destination = SingleTopLevelFolder(source)
                ? parent
                : FreeFolder(Path.Combine(parent, FolderNameFor(zip)));

            ExtractionResult? result = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                string? currentPassword = password;
                try
                {
                    result = await Task.Run(() => source.Extract(
                        [],
                        new ExtractionOptions
                        {
                            DestinationDirectory = destination,
                            Overwrite = OverwritePolicy.Rename,
                            Password = currentPassword,
                        },
                        progress,
                        token), token);
                    break;
                }
                catch (EncryptedArchiveException)
                {
                    password = PromptPassword(true);
                    if (password is null) return null;
                }
            }

            if (!quiet && result is not null)
            {
                Show(Loc.F("압축을 풀었습니다: 파일 {0:N0}개\n{1}", result.ExtractedFiles, destination));
            }

            return result is null ? null : new ArchiveExtraction(result, destination);
        }
    }

    /// <summary>"x.tar.gz" → "x" 처럼 이중 확장자를 함께 제거한 폴더 이름.</summary>
    internal static string FolderNameFor(string archivePath)
    {
        string name = Path.GetFileNameWithoutExtension(archivePath);
        if (name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return string.IsNullOrWhiteSpace(name) ? Loc.T("압축 풀기") : name;
    }

    /// <summary>모든 항목이 하나의 최상위 폴더 안에 있으면 그 폴더가 이미 래퍼 역할을 합니다.</summary>
    private static bool SingleTopLevelFolder(IArchiveSource source)
    {
        string? top = null;
        foreach (ArchiveEntryInfo entry in source.Entries)
        {
            string name = entry.Name.TrimStart('/');
            int slash = name.IndexOf('/');
            if (slash <= 0) return false;

            string first = name[..slash];
            if (top is null) top = first;
            else if (!string.Equals(top, first, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return top is not null;
    }

    internal static string FreeFolder(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path)) return path;

        for (int i = 2; i < 1000; i++)
        {
            string candidate = $"{path} ({i})";
            if (!Directory.Exists(candidate) && !File.Exists(candidate)) return candidate;
        }

        throw new IOException(Loc.T("풀 폴더 이름을 찾을 수 없습니다."));
    }

    private static string? PromptPassword(bool wrong)
    {
        var dialog = new PasswordDialog();
        if (wrong)
        {
            dialog.Title = Loc.T("비밀번호가 올바르지 않습니다 — 다시 입력");
        }

        return dialog.ShowDialog() == true ? dialog.Password : null;
    }

    private const string CreateCollectName = "WangZip.HereCreate";
    private const string ExtractCollectName = "WangZip.HereExtractAll";

    /// <summary>
    /// 탐색기는 여러 항목을 선택하면 항목마다 프로세스를 따로 띄웁니다.
    /// 먼저 뜬 프로세스가 잠시 기다리며 나머지 경로를 받아 한 번에 처리합니다.
    /// </summary>
    private static async Task CollectPathsAsync(
        string firstPath, string name, Func<IReadOnlyList<string>, Task> work)
    {
        using var mutex = new Mutex(true, @"Local\" + name, out bool leader);
        if (!leader)
        {
            try
            {
                using var client = new System.IO.Pipes.NamedPipeClientStream(".", name, System.IO.Pipes.PipeDirection.Out);
                await client.ConnectAsync(5000);
                using var writer = new StreamWriter(client, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
                await writer.WriteLineAsync(firstPath);
            }
            catch (Exception)
            {
                // 대표 프로세스가 이미 끝났다면 이 항목만 따로 처리합니다.
                await work([firstPath]);
            }

            return;
        }

        var paths = new List<string> { firstPath };
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(900);
        while (DateTime.UtcNow < deadline)
        {
            using var server = new System.IO.Pipes.NamedPipeServerStream(
                name, System.IO.Pipes.PipeDirection.In, 1,
                System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
            Task wait = server.WaitForConnectionAsync();
            Task finished = await Task.WhenAny(wait, Task.Delay(deadline - DateTime.UtcNow));
            if (finished != wait)
            {
                _ = wait.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                break;
            }

            using var reader = new StreamReader(server, new System.Text.UTF8Encoding(false));
            string? line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line)) paths.Add(line);
            deadline = DateTime.UtcNow.AddMilliseconds(600);
        }

        mutex.ReleaseMutex();
        await work(paths);
    }

    private static Task CollectAndCreateAsync(string firstPath) =>
        CollectPathsAsync(firstPath, CreateCollectName, RunCreateAsync);

    /// <summary>선택한 압축 파일을 모두 푭니다 (탐색기 "왕집: 모두 풀기").</summary>
    private static Task CollectAndExtractAllAsync(string firstPath) =>
        CollectPathsAsync(firstPath, ExtractCollectName, RunExtractAllAsync);

    private static async Task RunExtractAllAsync(IReadOnlyList<string> sourcePaths)
    {
        var normalized = sourcePaths.Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var archives = normalized.Where(File.Exists).ToList();
        var missing = normalized.Where(path => !File.Exists(path))
            .Select(path => Path.GetFileName(path) ?? path).ToList();

        if (archives.Count == 0)
        {
            Show(Loc.T("선택한 압축 파일을 찾을 수 없습니다."));
            return;
        }

        int extractedArchives = 0;
        int extractedFiles = 0;
        var failures = new List<string>(missing);

        bool ok = await ProgressDialog.RunAsync(null!, Loc.T("여러 압축 파일 푸는 중"), async (progress, token) =>
        {
            var batch = new BatchProgress(progress, archives.Count);
            for (int i = 0; i < archives.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                string name = Path.GetFileName(archives[i]);
                batch.Begin(i, name);
                try
                {
                    // quiet=true: 개별 완료 메시지상자 표시 안 함 (최종 요약만 표시)
                    ArchiveExtraction? done = await ExtractOneAsync(archives[i], batch, token, quiet: true);
                    if (done is null)
                    {
                        // 암호 입력을 취소했거나 열지 못한 경우
                        failures.Add(name);
                    }
                    else
                    {
                        extractedArchives++;
                        extractedFiles += done.Result.ExtractedFiles;
                    }
                }
                catch (EncryptedArchiveException)
                {
                    failures.Add(name);
                }
                catch (ArchiveException)
                {
                    failures.Add(name);
                }
                catch (UnauthorizedAccessException)
                {
                    failures.Add(name);
                }
                catch (IOException)
                {
                    failures.Add(name);
                }

                batch.Complete();
            }
        });

        if (!ok) return;

        string message = Loc.F("압축을 풀었습니다: {0:N0}개 압축 파일, 파일 {1:N0}개", extractedArchives, extractedFiles);
        if (failures.Count > 0)
        {
            message += "\n" + Loc.F("풀지 못한 압축 파일 {0}개: {1}", failures.Count, string.Join(", ", failures.Take(3)));
        }

        Show(message);
    }

    /// <summary>여러 압축 파일의 진행률을 하나의 진행 창에 합칩니다.</summary>
    private sealed class BatchProgress : IProgress<ProgressReport>
    {
        private readonly IProgress<ProgressReport> _inner;
        private readonly long _total;
        private int _index;

        public BatchProgress(IProgress<ProgressReport> inner, int count)
        {
            _inner = inner;
            _total = count * 100L;
        }

        public void Begin(int index, string name)
        {
            _index = index;
            _inner.Report(new ProgressReport(index * 100L, _total, name));
        }

        public void Complete()
        {
            _inner.Report(new ProgressReport(Math.Min(_total, (_index + 1) * 100L), _total, null));
        }

        public void Report(ProgressReport value)
        {
            long percent = (long)Math.Clamp(value.Percent, 0, 100);
            _inner.Report(new ProgressReport(Math.Min(_total, _index * 100L + percent), _total, value.CurrentName));
        }
    }

    private static async Task RunCreateAsync(IReadOnlyList<string> sourcePaths)
    {
        var sources = sourcePaths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (string item in sources)
        {
            if (!File.Exists(item) && !Directory.Exists(item))
            {
                throw new FileNotFoundException(Loc.T("파일이나 폴더를 찾을 수 없습니다."), item);
            }
        }

        string full = sources[0];
        string trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string directory = Path.GetDirectoryName(trimmed)
            ?? throw new IOException(Loc.T("이 위치(드라이브 최상위)에서는 압축 파일을 만들 수 없습니다. 하위 폴더를 선택해 주세요."));

        // 폴더 이름의 점(my.project)을 확장자로 오해하지 않습니다.
        // 여러 항목이면 그 항목들이 들어 있는 폴더 이름을 씁니다.
        string name = sources.Count > 1
            ? Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar))
            : File.Exists(full) ? Path.GetFileNameWithoutExtension(full) : Path.GetFileName(trimmed);
        string outputName = string.IsNullOrEmpty(name) ? Loc.T("압축") : name;
        string output = Path.Combine(directory, outputName + ".zip");

        int attempt = 1;
        while (File.Exists(output) || Directory.Exists(output))
        {
            attempt++;
            if (attempt > 999) throw new IOException(Loc.T("저장 이름을 찾을 수 없습니다."));
            output = Path.Combine(directory, $"{outputName}-{attempt}.zip");
        }

        CreationResult? created = null;
        bool ok = await ProgressDialog.RunAsync(null!, Loc.T("압축하는 중입니다"), async (progress, token) =>
        {
            created = await Task.Run(
                () => ZipWriter.Create(output, sources, new CreationOptions(), progress, token), token);
        });

        if (ok && created is not null)
        {
            Show(Loc.F("압축 파일을 만들었습니다:\n{0}\n({1:N0}개 항목)", created.OutputPath, created.EntryCount));
        }
    }

    private static void Show(string message)
    {
        MessageBox.Show(message, Loc.T("왕집"), MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
