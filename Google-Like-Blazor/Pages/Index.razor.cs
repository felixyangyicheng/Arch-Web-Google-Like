using System;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace Google_Like_Blazor.Pages
{
	public partial class Index
	{
        [Inject] RepositoryCache _repositoryCache { get; set; }
        [Inject] IFileRepo _file { get; set; }
        [Inject] MemoryStorageUtility MemoryStorageUtility { get; set; }
        /// <summary>
        /// loadinf indication
        /// </summary>
        public bool loading { get; set; } = false;
        /// <summary>
        /// chaine de caractères de recherche
        /// </summary>
        ///
        public string SearchWord { get; set; }
        public int FileCount { get; set; } = 0;
        public long elapsedMs { get; set; } = 0;
        public FileViewModel selectedItem { get; set; } = new();
        public List<FileViewModel> files { get; set; } = new();


        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                loading = true;
                var tempFiles = await _repositoryCache.GetFiles("tennis");
                files = tempFiles;
                FileCount = files.Count();
       
                loading = false;

                StateHasChanged();
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            FileCount = await _repositoryCache.GetFilesCount();
            StateHasChanged();

            loading = true;
            files =await _file.SearchInFileName("tennis");
            StateHasChanged();
            loading = false;

            await base.OnParametersSetAsync();
        }



        public async Task SetValue(string keyword, List<FileViewModel> fileViewModels)
        {
            MemoryStorageUtility.Storage[keyword] = fileViewModels;
        }

        public async Task<List<FileViewModel>> GetValueFromMemoryStorage(string keyword)
        {
            if (MemoryStorageUtility.Storage.TryGetValue(keyword, out var value))
            {
            
                return  (List<FileViewModel>) value;
            }
            else
            {
                var p = await _repositoryCache.GetFiles(keyword);
                StateHasChanged();
                SetValue(keyword, p);
                StateHasChanged();
                return p;
            }
        }

        public void ClearAll()
        {
            MemoryStorageUtility.Storage.Clear();
        }

        protected async Task InputChanged(string searchWord)
        {
            if (!string.IsNullOrWhiteSpace(searchWord))
            {
                FileCount = 0;
                elapsedMs = 0;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                // the code that you want to measure comes here

                loading = true;
                //files = await _file.SearchByNameAsync(searchWord);
                StateHasChanged();

                files = await _file.SearchInFileName(searchWord);
                
                loading = false;
                StateHasChanged();
       
                loading = true;
     

                var tempFiles = await GetValueFromMemoryStorage(searchWord);
                //var tempFiles = await _file.SearchInFileName(searchWord);
          
                files = tempFiles;
                FileCount = files.Count;
                StateHasChanged();

                if(FileCount<1){
                    files=await _repositoryCache.GetFiles(searchWord);
                }
                SearchWord = searchWord;
                loading = false;


                watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds;

                StateHasChanged();
            }

            InvokeAsync(StateHasChanged);
        }

        protected async Task ShowFile(string Id)
        {

        }


    }
}

