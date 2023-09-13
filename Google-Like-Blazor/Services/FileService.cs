

using Page = UglyToad.PdfPig.Content.Page;

namespace Google_Like_Blazor.Services
{
    public class FileService : IFileRepo
    {
        private readonly IMongoCollection<FileModel> _collection;
        private readonly ILogger<FileService> logger;
        public FileService(
            IOptions<ConnectionStringModel> dbSettings, ILogger<FileService> logger)
        {
            var mongoClient = new MongoClient(
                dbSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                dbSettings.Value.DatabaseName);

            _collection = mongoDatabase.GetCollection<FileModel>(
                dbSettings.Value.ThreedCollectionName);
            this.logger = logger;
        }

        public async Task<bool> CreateAsync(FileModel obj)
        {
            return _collection.InsertOneAsync(obj).IsCompletedSuccessfully;
        }

        public async Task<List<FileModel>> GetAsync() =>
            await _collection.Find(_ => true).ToListAsync();

        public async Task<FileModel?> GetAsync(string id) =>
            await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task<FileModel?> GetByNameAsync(string name) =>
            await _collection.Find(x => x.FileName == name).FirstOrDefaultAsync();



        public async Task RemoveAsync(string id) =>
            await _collection.DeleteOneAsync(x => x.Id == id);


        public async Task<List<FileViewModel>> SearchInContent(string keyword)
        { 
            var result = new List<FileViewModel>();

            logger.LogInformation("Created new list for result");
            var list= await _collection.Find(x=>x.Type.Contains("pdf")).ToListAsync();
            
      

                foreach (var item in list)
                {

                    var sb = new StringBuilder();

                    using (var pdfDocument = PdfDocument.Open(item.Content))
                    {
                        foreach (var page in pdfDocument.GetPages())
                        {
  
                            #region  Segment page
                            var pageSegmenter = DocstrumBoundingBoxes.Instance;
                            var pageSegmenterOptions = new DocstrumBoundingBoxes.DocstrumBoundingBoxesOptions(){ };
                            #endregion
                            foreach (Word word in page.GetWords())
                            {
                                if (word.Text.ToLower()==(keyword.ToLower()))
                                {



                                    var readingOrder = UnsupervisedReadingOrderDetector.Instance;
                                    var textBlocks = pageSegmenter.GetBlocks(page.GetWords());
                                
                                    var orderedTextBlocks = readingOrder.Get(textBlocks);

                                    foreach (var block in orderedTextBlocks)
                                    {
                                        logger.LogInformation($"Trace, {block.Text.Normalize(NormalizationForm.FormKC)}");

                                        sb.Append(block.Text.Normalize(NormalizationForm.FormKC)); // normalise text
                         
                                    }

                                }                              

                            }

                        }

                    }

                    FileViewModel vm = new FileViewModel
                    {
                        Id = item.Id,
                        Content = item.Content,
                        Type = item.Type,
                        TextToPreview = sb.ToString(),
                        FileName = item.FileName
                    };
                    result.Add(vm);
                    logger.LogInformation($"add viewModel in to result list {vm.TextToPreview}");
                }
             
            

            return result;
        }


        public async Task<List<FileModel>> SearchByNameAsync(string name) =>
            await _collection.Find(x => x.FileName.ToLowerInvariant().Contains( name.ToLowerInvariant())).ToListAsync();

        public async Task<bool> UpdateAsync(string id, FileModel obj)
        {
            return _collection.ReplaceOneAsync(x => x.Id == id, obj).IsCompletedSuccessfully;

        }
    }
}
