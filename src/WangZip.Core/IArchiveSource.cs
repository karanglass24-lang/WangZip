namespace WangZip.Core;

/// <summary>압축 파일을 읽기 위한 공통 인터페이스 (ZIP 직접 구현 + SharpCompress 백엔드).</summary>
public interface IArchiveSource : IDisposable
{
    string ArchivePath { get; }
    ArchiveFormat Format { get; }
    IReadOnlyList<ArchiveEntryInfo> Entries { get; }
    IReadOnlyList<string> Warnings { get; }

    /// <summary>파일명 인코딩 감지 결과 설명 (예: "CP949(자동)").</summary>
    string EncodingDescription { get; }

    ExtractionResult Extract(
        IReadOnlyCollection<int> indices,
        ExtractionOptions options,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken);
}
