

namespace Google_Like_Blazor.Services
{
    public interface IFileRepo:IMongoReposBase<FileModel>
    {
        public Task<FileModel?> GetByNameAsync(string name);

        Task<List<FileViewModel>> SearchInContent(string keyword);
        Task<List<FileViewModel>> SearchInFileName(string keyword);
    }
}
