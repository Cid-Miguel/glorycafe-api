namespace GloryCafe.Application.Common.Interfaces;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativeUrl, CancellationToken cancellationToken = default);
}
