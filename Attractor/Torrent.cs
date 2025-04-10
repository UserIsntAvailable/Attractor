using System.Numerics;
using OneOf;

namespace Attractor;

/// <summary>
/// A Metainfo file (also known as .torrent file)
/// </summary>
///
/// <param name="Announce">The URL of the tracker</param>
public record Torrent(Uri Announce, Info Info)
{
    /// <summary>
    /// Parses a <see cref="Torrent"/> from the <paramref name="stream"/>.
    /// </summary>
    public static OneOf<Torrent, ParsingError> Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var torrentResult = BValue.Parse(stream);
        if (torrentResult.TryPickT1(out var error, out var torrentOk))
        {
            return ParsingError.FormatException("Invalid torrent file.", error);
        }
        if (!torrentOk.TryPickT3(out var torrent, out var _))
        {
            return ParsingError.FormatException("Torrent files are bencoded directories.");
        }

        var announceValue = torrent["announce"];
        if (announceValue is null)
        {
            return ParsingError.FormatException("Missing 'announce' key.");
        }
        if (!announceValue.TryPickT0(out var announceString, out var _))
        {
            return ParsingError.FormatException("The 'announce' key should have a 'string' value.");
        }
        // FIXME(Unavailable): try `UriFormatException`.
        var announce = new Uri(announceString.AsString());

        var infoValue = torrent["info"];
        if (infoValue is null)
        {
            return ParsingError.FormatException("Missing 'info' key");
        }
        if (!infoValue.TryPickT3(out var info, out var _))
        {
            return ParsingError.FormatException("The 'info' key should have a 'dictionary' value.");
        }

        var nameValue = info["name"];
        if (nameValue is null)
        {
            return ParsingError.FormatException("Missing 'info.name' key.");
        }
        if (!nameValue.TryPickT0(out var name, out var _))
        {
            return ParsingError.FormatException(
                "The 'info.name' key should have a 'string' value."
            );
        }

        var pieceLengthValue = info["piece length"];
        if (pieceLengthValue is null)
        {
            return ParsingError.FormatException("Missing 'info.piece length' key.");
        }
        if (!pieceLengthValue.TryPickT1(out var pieceLength, out var _))
        {
            return ParsingError.FormatException(
                "The 'info.piece length' key should have an 'integer' value."
            );
        }

        var piecesValue = info["pieces"];
        if (piecesValue is null)
        {
            return ParsingError.FormatException("Missing 'info.pieces' key.");
        }
        if (!piecesValue.TryPickT0(out var piecesBytes, out var _))
        {
            return ParsingError.FormatException(
                "The 'info.pieces' key should have an 'string' value."
            );
        }
        if (piecesBytes.Bytes.Length % 20 != 0)
        {
            return ParsingError.FormatException(
                "The 'info.pieces' key should have a size that is multiple of '20'."
            );
        }

        // PERF(Unavailable): This surely could be written better.
        var pieces = piecesBytes.AsString().Chunk(20).Select(static (x) => new string(x)).ToList();

        var singleFileLength = info["length"];
        var multiFileFiles = info["files"];

        if (singleFileLength is null && multiFileFiles is null)
        {
            return ParsingError.FormatException(
                "A 'info.length' or 'info.files' key should be provided."
            );
        }
        if (singleFileLength is not null && multiFileFiles is not null)
        {
            return ParsingError.FormatException(
                "Both 'info.length' and 'info.files' keys can't be provided at the same time."
            );
        }

        FileKind fileKind;
        if (
            singleFileLength is not null
            && singleFileLength.TryPickT1(out var singleLength, out var _)
        )
        {
            fileKind = new SingleFile(singleLength);
        }
        else if (multiFileFiles is not null && multiFileFiles.TryPickT2(out var files, out var _))
        {
            if (files.Values.Count == 0)
            {
                return ParsingError.FormatException(
                    "The 'info.files' value can't have zero elements."
                );
            }

            List<File> mappedFiles = new(files.Values.Count);
            foreach (var (fileIndex, file) in files.Index())
            {
                if (!multiFileFiles.TryPickT3(out var fileDict, out var _))
                {
                    return ParsingError.FormatException(
                        $"The 'info.files[{fileIndex}]' key should have a 'dictionary' value."
                    );
                }

                var lengthValue = info["length"];
                if (lengthValue is null)
                {
                    return ParsingError.FormatException(
                        $"Missing 'info.files[{fileIndex}].length' key."
                    );
                }
                if (!lengthValue.TryPickT1(out var multiLength, out var _))
                {
                    return ParsingError.FormatException(
                        $"The 'info.files[{fileIndex}].length' key should have a 'integer' value."
                    );
                }

                var pathValue = info["path"];
                if (pathValue is null)
                {
                    return ParsingError.FormatException(
                        $"Missing 'info.files[{fileIndex}].path' key."
                    );
                }
                if (!pathValue.TryPickT2(out var path, out var _))
                {
                    return ParsingError.FormatException(
                        $"The 'info.files[{fileIndex}].path' key should have a 'list' value."
                    );
                }

                List<string> mappedPaths = new(path.Values.Count);
                foreach (var (dirNameIndex, dirNameValue) in path.Index())
                {
                    if (!dirNameValue.TryPickT0(out var dirName, out var _))
                    {
                        return ParsingError.FormatException(
                            $"The 'info.files[{fileIndex}].path[{dirNameIndex}]' key should have a 'string' value."
                        );
                    }
                    mappedPaths.Add(dirName.AsString());
                }

                mappedFiles.Add(new File(multiLength, mappedPaths));
            }

            fileKind = new MultiFile(mappedFiles);
        }
        else
        {
            return singleFileLength is null
                ? ParsingError.FormatException(
                    "The 'info.length' key should have a 'string' value."
                )
                : ParsingError.FormatException("The 'info.files' key should have a 'list' value.");
        }

        return new Torrent(announce, new Info(name.AsString(), pieceLength, pieces, fileKind));
    }
}

/// <param name="Name">
/// The suggested name to save the file (or directory) as. It is purely advisory.
///
/// In the single file case, the name key is the name of a file, in the muliple
/// file case, it's the name of a directory.
/// </param>
///
/// <param name="PieceLength">
/// The number of bytes in each piece the file is split into. For the purposes
/// of transfer, files are split into fixed-size pieces which are all the same
/// length except for possibly the last one which may be truncated.
///
/// This value is almost always a power of two, most commonly 2^18 = 256K;
/// BitTorrent prior to version 3.2 uses 2^20 = 1M as default.
/// </param>
///
/// <param name="Pieces">
/// A list of strings which are of length 20, where each of which is the SHA1
/// hash of the piece at the corresponding index.
/// </param>
public record Info(string Name, BigInteger PieceLength, List<string> Pieces, FileKind FileKind) { }

/// <summary>
/// There is also a key length or a key files, but not both or neither. If
/// length is present then the download represents a single file, otherwise it
/// represents a set of files which go in a directory structure.
/// </summary>
[GenerateOneOf]
public partial class FileKind : OneOfBase<SingleFile, MultiFile>
{
    // TODO(Unavailable): As/TryPick{SingleFile, MultiFile}
}

/// <param name="Length">
/// The length of the file, in bytes.
/// </param>
public record struct SingleFile(BigInteger Length);

/// <summary>
/// For the purposes of the other keys, the multi-file case is treated as only
/// having a single file by concatenating the files in the order they appear in
/// the files list.
/// </summary>
///
/// <param name="Files">
/// The list of file to process.
/// </param>
public record MultiFile(List<File> Files);

/// <summary>
/// A file in the multi-file case.
/// </summary>
///
/// <param name="Length">
/// The length of the file, in bytes.
/// </param>
///
/// <param name="Path">
/// Subdirectory names for this file, the last of which is the actual file name
/// (a zero length list is an error case).
/// </param>
public record File(BigInteger Length, List<string> Path);
