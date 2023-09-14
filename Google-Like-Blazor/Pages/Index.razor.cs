using System;
using System.Runtime.Serialization.Formatters.Binary;

namespace Google_Like_Blazor.Pages
{
	public partial class Index
	{
        [Inject] RepositoryCache _repositoryCache { get; set; }
        [Inject] IFileRepo _file { get; set; }
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

        protected async Task InputChanged(string searchWord)
        {
            if (!string.IsNullOrWhiteSpace(searchWord))
            {

                loading = true;
                //files = await _file.SearchByNameAsync(searchWord);
                StateHasChanged();

                files = await _file.SearchInFileName(searchWord);
                loading = false;
                StateHasChanged();

                loading = true;
                StateHasChanged();

                var tempFiles = await _repositoryCache.GetFiles(searchWord);
                files = tempFiles;
                FileCount = files.Count;
                SearchWord = searchWord;
                loading = false;

                StateHasChanged();
            }

            InvokeAsync(StateHasChanged);
        }

        protected async Task ShowFile(string Id)
        {

        }


    }
}

