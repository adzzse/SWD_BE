namespace BLL.Interface
{
	public interface IArchiveExtractionService
	{
		Task ExtractToDirectoryAsync(string archivePath, string destinationPath, CancellationToken cancellationToken = default);
	}
}
