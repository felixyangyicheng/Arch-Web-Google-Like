using MongoDB.Bson;
using MongoDB.Driver.GridFS;
using System;
namespace Google_Like_Blazor.Services
{
    public interface IGridSfRepo
    {
        public Task<ObjectId> UploadFile(string fileName, string fileExtention, byte[] file);
        public Task DeleteFile(ObjectId id);
        public Task<byte[]> DownloadAsBytesByFileName(string fileName);
        public Task<byte[]> DownloadAsBytes(ObjectId id);
        public Task<GridFSFileInfo> FindFiles(string fileName);
        public Task<IList<GridFSFileInfo>> SearchFilesByKeyword(string fileName);

    }
}

