

using System.Text;
using UglyToad.PdfPig.Core;
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

                            var words = page.GetWords();
                            foreach (Word word in words)
                            {
                                if (word.Text.ToLower().Contains(keyword.ToLower()))
                                {
                                    List<Word> filtred = words.Where(x => x.Text.ToLower().Contains(keyword.ToLower())).ToList();
                                logger.LogInformation($"Trace filtred, {filtred.Count()}");


                                var readingOrder = UnsupervisedReadingOrderDetector.Instance;
                             

                                    var textBlocks = pageSegmenter.GetBlocks(filtred);
                                    logger.LogInformation($"Trace textBlocks, {textBlocks.Count()}");
                                foreach (var tb in textBlocks)
                                {
                                    logger.LogInformation($"Trace tb, {tb.Text}");

                                }


                                var orderedTextBlocks = readingOrder.Get(textBlocks);
                               
                                    foreach (var block in orderedTextBlocks)
                                    {

                                        logger.LogInformation($"Trace orderedTextBlocks, {orderedTextBlocks.Count()}");
                                   

                                    logger.LogInformation($"Trace block text, {block.Text.Normalize(NormalizationForm.FormKC)}");

                                        var areaWithoutBorders = block.BoundingBox;
                                        var lines = block.TextLines;
                                        logger.LogInformation($"Trace lines, {lines.Count()}");

                                    foreach (var t in lines)
                                    {
                                        logger.LogInformation($"Trace, {t.Text.Normalize(NormalizationForm.FormKC)}");

                                        var pageText = string.Join(" ", t.Text.Normalize(NormalizationForm.FormKC));                       
                                        sb.AppendJoin(" ", pageText);
                                        //sb.Append(pageText);
                                    }
                                  //  sb.Append(block.Text.Normalize(NormalizationForm.FormKC)); // normalise text
                         
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
                if (vm.TextToPreview.Contains(keyword))
                {

                result.Add(vm);
                }
                logger.LogInformation($"add viewModel in to result list {vm.FileName}");
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
