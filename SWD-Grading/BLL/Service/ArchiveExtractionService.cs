using BLL.Interface;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace BLL.Service
{
	public class ArchiveExtractionService : IArchiveExtractionService
	{
		private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".zip",
			".rar"
		};

		public Task ExtractToDirectoryAsync(string archivePath, string destinationPath, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(archivePath))
			{
				throw new ArgumentException("Archive path is required.", nameof(archivePath));
			}

			var extension = Path.GetExtension(archivePath);
			if (!SupportedExtensions.Contains(extension))
			{
				throw new ArgumentException($"File type {extension} is not allowed. Only .zip and .rar files are accepted.");
			}

			if (!File.Exists(archivePath))
			{
				throw new FileNotFoundException("Archive file was not found.", archivePath);
			}

			Directory.CreateDirectory(destinationPath);

			using var archive = ArchiveFactory.Open(archivePath);
			foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
			{
				cancellationToken.ThrowIfCancellationRequested();
				entry.WriteToDirectory(destinationPath, new ExtractionOptions
				{
					ExtractFullPath = true,
					Overwrite = true
				});
			}

			return Task.CompletedTask;
		}
	}
}
