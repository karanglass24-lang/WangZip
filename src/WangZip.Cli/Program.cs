using System.Text;
using System.Text.Json;
using System.IO.Compression;
using WangZip.Core;
using WangZip.Core.Zip;

namespace WangZip.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        try
        {
            return Run(args);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("작업이 취소되었습니다.");
            return 3;
        }
        catch (ArchiveException exception)
        {
            Console.Error.WriteLine("오류: " + exception.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("예외: " + exception);
            return 2;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        string command = args[0].ToLowerInvariant();
        string[] rest = args.Skip(1).ToArray();

        return command switch
        {
            "list" or "l" => List(rest),
            "extract" or "x" => Extract(rest),
            "create" or "c" => Create(rest),
            "verify" or "t" => Verify(rest),
            "version" or "--version" => Version(),
            _ => Unknown(command),
        };
    }

    private static int List(string[] args)
    {
        if (args.Length == 0) return Usage("압축 파일 경로가 필요합니다.");

        string archivePath = args[0];
        NameEncodingMode encoding = ParseEncoding(Value(args, "--encoding"));
        string? password = Value(args, "--password");
        bool json = Has(args, "--json");
        bool quiet = Has(args, "--quiet");

        using IArchiveSource source = OpenArchiveInteractive(archivePath, encoding, password, quiet);

        if (json)
        {
            var payload = new
            {
                path = source.ArchivePath,
                format = ArchiveService.FormatDisplayName(source.Format),
                encoding = source.EncodingDescription,
                entries = source.Entries.Select(entry => new
                {
                    index = entry.Index,
                    name = entry.Name,
                    size = entry.Size,
                    compressedSize = entry.CompressedSize,
                    method = entry.MethodName,
                    modified = entry.Modified == default ? null : entry.Modified.ToString("O"),
                    isDirectory = entry.IsDirectory,
                    isEncrypted = entry.IsEncrypted,
                }),
            };

            Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine($"압축 파일: {source.ArchivePath}");
        Console.WriteLine($"형식: {ArchiveService.FormatDisplayName(source.Format)} / 파일명 인코딩: {source.EncodingDescription}");

        foreach (string warning in source.Warnings)
        {
            Console.WriteLine("  ! " + warning);
        }

        Console.WriteLine();
        Console.WriteLine($"{"번호",6}  {"크기",12}  {"압축",12}  {"압축률",7}  {"수정일",16}  이름");
        Console.WriteLine(new string('-', 100));

        foreach (ArchiveEntryInfo entry in source.Entries)
        {
            string mark = entry.IsEncrypted ? " 🔒" : string.Empty;
            Console.WriteLine(
                $"{entry.Index,6}  {entry.SizeText,12}  {entry.CompressedText,12}  " +
                $"{entry.RatioText,7}  {entry.ModifiedText,16}  {entry.Name}{mark}");
        }

        long totalSize = source.Entries.Sum(entry => entry.Size);
        long totalCompressed = source.Entries.Sum(entry => entry.CompressedSize);
        double ratio = totalSize <= 0 ? 0 : 1.0 - (double)totalCompressed / totalSize;

        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"{source.Entries.Count}개 항목, 원본 {Format.Bytes(totalSize)}, 압축 {Format.Bytes(totalCompressed)}, {Format.CompressionRate(ratio)}");
        return 0;
    }

    private static int Extract(string[] args)
    {
        if (args.Length == 0) return Usage("압축 파일 경로가 필요합니다.");

        string archivePath = args[0];
        string? destination = null;
        if (args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal))
        {
            destination = args[1];
        }

        NameEncodingMode encoding = ParseEncoding(Value(args, "--encoding"));
        OverwritePolicy overwrite = ParseOverwrite(Value(args, "--overwrite"));
        string? selectText = Value(args, "--select");
        string? password = Value(args, "--password");
        bool quiet = Has(args, "--quiet");

        using IArchiveSource source = OpenArchiveInteractive(archivePath, encoding, password, quiet);

        destination ??= Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(archivePath))!,
            Path.GetFileNameWithoutExtension(archivePath));

        var selected = new List<int>();
        if (!string.IsNullOrEmpty(selectText))
        {
            var wanted = new HashSet<string>(
                selectText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);

            foreach (ArchiveEntryInfo entry in source.Entries)
            {
                if (wanted.Contains(entry.Name)) selected.Add(entry.Index);
            }

            if (selected.Count == 0)
            {
                Console.Error.WriteLine("선택한 항목을 찾지 못했습니다.");
                return 2;
            }
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        var progress = quiet ? null : new ConsoleProgress();
        ExtractionResult result = source.Extract(
            selected,
            new ExtractionOptions
            {
                DestinationDirectory = destination,
                Overwrite = overwrite,
                Password = password,
            },
            progress,
            cancellation.Token);

        (progress as ConsoleProgress)?.Complete();

        Console.WriteLine($"압축을 풀었습니다: {result.ExtractedFiles}개 파일, {Format.Bytes(result.TotalBytes)}");
        if (result.SkippedFiles > 0) Console.WriteLine($"건너뜀: {result.SkippedFiles}개");
        Console.WriteLine($"대상 폴더: {destination}");
        Console.WriteLine($"소요 시간: {result.Elapsed.TotalSeconds:0.00}초");

        foreach (string warning in result.Warnings)
        {
            Console.WriteLine("  ! " + warning);
        }

        return 0;
    }

    private static int Create(string[] args)
    {
        if (args.Length < 2) return Usage("출력 파일과 압축할 대상이 필요합니다.");

        string outputPath = args[0];
        var sources = new List<string>();
        foreach (string arg in args.Skip(1))
        {
            if (arg.StartsWith("--", StringComparison.Ordinal)) break;
            sources.Add(arg);
        }

        if (sources.Count == 0) return Usage("압축할 파일이나 폴더가 필요합니다.");

        CompressionLevel level = ParseLevel(Value(args, "--level"));
        NameEncodingMode encoding = ParseEncoding(Value(args, "--encoding"));
        string? comment = Value(args, "--comment");
        bool quiet = Has(args, "--quiet");

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        var progress = quiet ? null : new ConsoleProgress();
        CreationResult result = ZipWriter.Create(
            outputPath,
            sources,
            new CreationOptions
            {
                Level = level,
                NameEncoding = encoding,
                Comment = comment,
            },
            progress,
            cancellation.Token);

        (progress as ConsoleProgress)?.Complete();

        Console.WriteLine($"압축 파일을 만들었습니다: {result.OutputPath}");
        Console.WriteLine($"{result.EntryCount}개 항목, 원본 {Format.Bytes(result.SourceBytes)} → {Format.Bytes(result.OutputBytes)} ({Format.CompressionRate(result.Ratio)})");

        foreach (string warning in result.Warnings)
        {
            Console.WriteLine("  ! " + warning);
        }

        return 0;
    }

    private static int Verify(string[] args)
    {
        if (args.Length == 0) return Usage("압축 파일 경로가 필요합니다.");

        string archivePath = args[0];
        NameEncodingMode encoding = ParseEncoding(Value(args, "--encoding"));
        string? password = Value(args, "--password");
        bool quiet = Has(args, "--quiet");

        using IArchiveSource source = OpenArchiveInteractive(archivePath, encoding, password, quiet);
        if (source is not ZipArchiveSource zip)
        {
            Console.WriteLine("검사는 ZIP 형식에서만 지원합니다. 항목 목록은 정상적으로 읽혔습니다.");
            return 0;
        }

        int checkedCount = zip.Verify(null, CancellationToken.None, password);
        Console.WriteLine($"정상입니다. {checkedCount}개 항목의 CRC를 확인했습니다.");
        return 0;
    }

    private static int Version()
    {
        Console.WriteLine("WangZip CLI 0.4.0 (GPL-3.0)");
        Console.WriteLine("압축 엔진: 직접 구현 ZIP + SharpCompress");
        return 0;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"알 수 없는 명령입니다: {command}");
        PrintHelp();
        return 1;
    }

    /// <summary>암호 걸린 압축 파일일 때 화면에서 비밀번호를 한 번 묻고 다시 엽니다.</summary>
    private static IArchiveSource OpenArchiveInteractive(
        string archivePath, NameEncodingMode encoding, string? password, bool quiet)
    {
        while (true)
        {
            try
            {
                return ArchiveService.Open(archivePath, encoding, password);
            }
            catch (EncryptedArchiveException) when (password is null && !Console.IsOutputRedirected)
            {
                if (quiet)
                {
                    throw;
                }

                Console.Error.WriteLine("이 압축 파일은 비밀번호가 필요합니다. (--password 옵션으로도 지정 가능)");
                Console.Error.Write("비밀번호: ");
                string? typed = Console.ReadLine();
                if (string.IsNullOrEmpty(typed))
                {
                    throw;
                }

                password = typed;
            }
        }
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            WangZip - 광고 없는 오픈소스 압축 프로그램 (v0.4)

            사용법:
              wangzip list    <압축파일> [--encoding auto|utf8|cp949|raw] [--json] [--password 비밀번호]
              wangzip extract <압축파일> [대상폴더] [--encoding ...] [--overwrite overwrite|skip|rename] [--password 비밀번호]
              wangzip create  <출력.zip> <파일|폴더>... [--level none|fastest|optimal|smallest] [--encoding utf8|cp949] [--comment "설명"]
              wangzip verify  <압축파일> [--password 비밀번호]

            예시:
              wangzip list "사진.zip"
              wangzip extract "사진.zip" "C:\Temp\사진" --encoding cp949
              wangzip extract "비밀.zip" "C:\Temp\비밀" --password "1234"
              wangzip create "백업.zip" "C:\작업" --level optimal
            """);
    }

    private static string? Value(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }

        return null;
    }

    private static bool Has(string[] args, string name) =>
        args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    private static NameEncodingMode ParseEncoding(string? value) => (value ?? "auto").ToLowerInvariant() switch
    {
        "auto" => NameEncodingMode.Auto,
        "utf8" or "utf-8" => NameEncodingMode.Utf8,
        "cp949" or "euc-kr" or "euckr" or "949" => NameEncodingMode.Cp949,
        "raw" => NameEncodingMode.Raw,
        _ => throw new ArchiveException($"알 수 없는 인코딩입니다: {value}"),
    };

    private static OverwritePolicy ParseOverwrite(string? value) => (value ?? "overwrite").ToLowerInvariant() switch
    {
        "overwrite" or "o" => OverwritePolicy.Overwrite,
        "skip" or "s" => OverwritePolicy.Skip,
        "rename" or "r" => OverwritePolicy.Rename,
        _ => throw new ArchiveException($"알 수 없는 덮어쓰기 방식입니다: {value}"),
    };

    private static CompressionLevel ParseLevel(string? value) => (value ?? "optimal").ToLowerInvariant() switch
    {
        "none" or "store" or "0" => CompressionLevel.NoCompression,
        "fastest" or "fast" or "1" => CompressionLevel.Fastest,
        "optimal" or "normal" or "5" => CompressionLevel.Optimal,
        "smallest" or "max" or "9" => CompressionLevel.SmallestSize,
        _ => throw new ArchiveException($"알 수 없는 압축 수준입니다: {value}"),
    };

    private sealed class ConsoleProgress : IProgress<ProgressReport>
    {
        private int _lastPercent = -1;

        public void Report(ProgressReport value)
        {
            int percent = (int)value.Percent;
            if (percent == _lastPercent) return;
            _lastPercent = percent;

            string name = value.CurrentName ?? string.Empty;
            if (name.Length > 40) name = "..." + name[^37..];

            Console.Write($"\r{percent,3}%  {name,-40}");
        }

        public void Complete()
        {
            Console.Write("\r" + new string(' ', 60) + "\r");
        }
    }
}
