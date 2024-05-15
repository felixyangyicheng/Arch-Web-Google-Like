

using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MongoDB.Driver;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;
using static MudBlazor.CategoryTypes;
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

        public FileService(IMongoCollection<FileModel> collection)
        {
            _collection = collection;
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

            var list= await _collection.Find(x=>x.Type.Contains("pdf")).ToListAsync();           
            foreach (var item in list)
            {
                var sb = new StringBuilder();
                using (var pdfDocument = PdfDocument.Open(item.Content))
                { 
                    foreach (var page in pdfDocument.GetPages())
                    {                                
                        var words = page.GetWords();                
                        var text = page.Text;             
                        var r = new Regex(@"[^.!?;]*" + keyword + @"[^.!?;]*");
                        var m = r.Matches(text);
                        var res = Enumerable.Range(0, m.Count).Select(index => m[index].Value).ToList();
                        foreach (var itm in res)
                        {
                            string CleanedString = Regex.Replace(itm, keyword, $"<span class='keyword'>{keyword}</span>");
                            sb.Append("<p> [page " + page.Number+ "] << " + CleanedString + " >> </p>");                        
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
                if (vm.TextToPreview.ToLowerInvariant().Contains(keyword.ToLowerInvariant()) ||vm.FileName.ToLowerInvariant().Contains(keyword.ToLowerInvariant()))
                {                    
                    result.Add(vm);
                }
     
            }
            return result;
        }

        public async Task<List<FileModel>> SearchByNameAsync(string name) =>
            await _collection.Find(x => x.FileName.ToLowerInvariant().Contains( name.ToLowerInvariant()) && x.Type.Contains("pdf")).ToListAsync();

        public async Task<bool> UpdateAsync(string id, FileModel obj)
        {
            return _collection.ReplaceOneAsync(x => x.Id == id, obj).IsCompletedSuccessfully;

        }


        #region SearchInFileName

        public async Task<List<FileViewModel>> SearchInFileName(string keyword)
        {
            var result = new List<FileViewModel>();

            logger.LogInformation("Created new list for result");
            var list = await _collection.Find(x => x.FileName.ToLowerInvariant().Contains(keyword.ToLowerInvariant()) && x.Type.Contains("pdf")).ToListAsync();

             result = (from x in list
                          select new FileViewModel
                          {
                              Id = x.Id,
                              Content = x.Content,
                              Type = x.Type,
                              TextToPreview = "",
                              FileName = x.FileName
                          }
                        ).ToList();

            return result;
        }
        #endregion

        public async Task<List<FileViewModel>> SearchInContentParelle(string keyword)
        {
            var result = new List<FileViewModel>();

            var list = await _collection.Find(x => x.Type.Contains("pdf")).ToListAsync();

            Parallel.ForEach(list, async item =>
            {
                var sb = new StringBuilder();
                PdfDocument pdfDocument = null;
                try
                {
                    pdfDocument = PdfDocument.Open(item.Content);
                    foreach (var page in pdfDocument.GetPages())
                    {
                        var words = page.GetWords();
                        var text = page.Text;
                        var r = new Regex(@"[^.!?;]*" + keyword + @"[^.!?;]*");
                        var m = r.Matches(text);
                        var res = Enumerable.Range(0, m.Count).Select(index => m[index].Value).ToList();
                        foreach (var itm in res)
                        {
                            string CleanedString = Regex.Replace(itm, keyword, $"<span class='keyword'>{keyword}</span>");
                            sb.Append("<p> [page " + page.Number + "] << " + CleanedString + " >> </p>");
                        }
                    }
                }
                finally
                {
                    if (pdfDocument != null)
                        pdfDocument.Dispose();
                }
                FileViewModel vm = new FileViewModel
                {
                    Id = item.Id,
                    Content = item.Content,
                    Type = item.Type,
                    TextToPreview = sb.ToString(),
                    FileName = item.FileName
                };
                if (vm.TextToPreview.ToLowerInvariant().Contains(keyword.ToLowerInvariant()) || vm.FileName.ToLowerInvariant().Contains(keyword.ToLowerInvariant()))
                {
                    lock (result)
                    {
                        result.Add(vm);
                    }
                }
            });

            return result;
        }

        public async Task<List<FileViewModel>> SearchInContentTask(string keyword)
        {
            var result = new List<FileViewModel>();
            var fileList = await _collection.Find(x => x.Type.Contains("pdf")).ToListAsync();
            var tasks = fileList.Select(async item =>
            {
                var sb = new StringBuilder();
                using (var pdfDocument = PdfDocument.Open(item.Content))
                {
                    var searchTasks = pdfDocument.GetPages().Select(async page =>
                    {
                        var words = page.GetWords();
                        var text = page.Text;
                        var r = new Regex(@"[^.!?;]*" + keyword + @"[^.!?;]*");
                        var m = r.Matches(text);
                        var res = Enumerable.Range(0, m.Count).Select(index => m[index].Value).ToList();
                        foreach (var itm in res)
                        {
                            string CleanedString = Regex.Replace(itm, keyword, $"<span class='keyword'>{keyword}</span>");
                            sb.Append("<p> [page " + page.Number + "] << " + CleanedString + " >> </p>");
                        }
                    });
                    await Task.WhenAll(searchTasks); // Wait for all search tasks to complete
                }
                FileViewModel vm = new FileViewModel
                {
                    Id = item.Id,
                    Content = item.Content,
                    Type = item.Type,
                    TextToPreview = sb.ToString(),
                    FileName = item.FileName
                };
                if (vm.TextToPreview.ToLowerInvariant().Contains(keyword.ToLowerInvariant()) || vm.FileName.ToLowerInvariant().Contains(keyword.ToLowerInvariant()))
                {
                    lock (result) // Ensure thread safety when accessing shared list
                    {
                        result.Add(vm);
                    }
                }
            });
            await Task.WhenAll(tasks);
            return result;
        }

        public IAsyncEnumerable<FileViewModel> SearchInContentAsyncEnum(string keyword)
        {
            throw new NotImplementedException();
        }
    }
}
