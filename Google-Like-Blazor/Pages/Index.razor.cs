using System;
using System.Runtime.Serialization.Formatters.Binary;

namespace Google_Like_Blazor.Pages
{
	public partial class Index
	{
        [Inject] IFileRepo _file { get; set; }
        /// <summary>
        /// loadinf indication
        /// </summary>
        public bool loading { get; set; } = false;
        /// <summary>
        /// chaine de caractères de recherche
        /// </summary>
        ///
        public string searchWord { get; set; }

        public FileViewModel selectedItem { get; set; } = new();
        public List<FileViewModel> files { get; set; } = new();

        protected override async Task OnParametersSetAsync()
        {
            loading = true;
            files =await _file.SearchInContent("climatique");
            StateHasChanged();
            loading = false; ;
            await base.OnParametersSetAsync();
        }

        protected async Task InputChanged(string searchWord)
        {
            loading = true;
            //files = await _file.SearchByNameAsync(searchWord);

            files = await _file.SearchInContent(searchWord);
            loading = false;
            StateHasChanged();
            InvokeAsync(StateHasChanged);
        }

        protected async Task ShowFile(string Id)
        {

        }


    }
}

