using System;


namespace Google_Like_Blazor.Services
{
    public class GridSfService : IGridSfRepo
    {
        public readonly IMongoClient mongoClient;
        //public readonly IMongoDatabase mongoDb;
        private IMongoDatabase mongoDatabase;
        public GridSfService(IOptions<ConnectionStringModel> dbSettings)
        {
            var mongoClient = new MongoClient(
                dbSettings.Value.ConnectionString);

            mongoDatabase = mongoClient.GetDatabase(
                dbSettings.Value.DatabaseName);

        }
        public GridSfService(IMongoClient mongoClient)
        {
            mongoClient = mongoClient;
        }
        /// <summary>
        /// Supprimer fichier
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task DeleteFile(ObjectId id)
        {
            var bucket = new GridFSBucket(mongoDatabase);
            await bucket.DeleteAsync(id);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<byte[]> DownloadAsBytes(ObjectId id)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<byte[]> DownloadAsBytesByFileName(string fileName)
        {
            var bucket = new GridFSBucket(mongoDatabase);

            return bucket.DownloadAsBytesByName(fileName);
        }

        public async Task<GridFSFileInfo> FindFiles(string fileName)
        {
            var bucket = new GridFSBucket(mongoDatabase);
            var filter = Builders<GridFSFileInfo>.Filter.And(
                            Builders<GridFSFileInfo>.Filter.Where(x => x.Filename == fileName));
            var sort = Builders<GridFSFileInfo>.Sort.Descending(x => x.UploadDateTime);
            var options = new GridFSFindOptions
            {
                Limit = 1,
                Sort = sort
            };

            var cursor = await bucket.FindAsync(filter, options);

            return (await cursor.ToListAsync()).FirstOrDefault();
            // fileInfo either has the matching file information or is null

        }


        public async Task<IList<GridFSFileInfo>> SearchFilesByKeyword(string fileName)
        {
            var bucket = new GridFSBucket(mongoDatabase);
            var filter = Builders<GridFSFileInfo>.Filter.And(
                            Builders<GridFSFileInfo>.Filter.Where(x => 
                            x.Filename.Contains(fileName)||x.Metadata.Contains(fileName)));
            var sort = Builders<GridFSFileInfo>.Sort.Descending(x => x.Filename);
            var options = new GridFSFindOptions
            {
                Limit = 100,
                Sort = sort
            };
            var cursor = await bucket.FindAsync(filter, options);
            return await cursor.ToListAsync();
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="fileExtention"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public async Task<ObjectId> UploadFile(string fileName, string fileExtention, byte[] file)
        {
            var bucket = new GridFSBucket(mongoDatabase);



            var options = new GridFSUploadOptions
            {
                ChunkSizeBytes = 524288, // 63KB
                
                Metadata = new BsonDocument
                {
                    { "resolution", "1080P" },
                    { "fileName", fileName },
                    { "type", fileExtention },
                    { "copyrighted", true }
                }
            };
            var id = await bucket.UploadFromBytesAsync(fileName, file, options);
            return id;
        }


    }
}

