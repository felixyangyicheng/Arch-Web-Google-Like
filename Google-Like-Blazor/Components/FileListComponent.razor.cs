using System;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace Google_Like_Blazor.Components
{
	public partial class FileListComponent
	{

        #region DI



        [Inject] IFileRepo _file { get; set; }
        [Inject] ILocalStorageService _localStorage { get; set; }

        [Inject] IJSRuntime JsRuntime { get; set; }
        [Inject] IDialogService DialogService { get; set; }
        [Inject] NavigationManager _nav { get; set; }


        #endregion
        #region Properties
 

        public List<FileViewModel> FileViewModels;
        public FileViewModel RecentFileViewModel = new();
        public FileViewModel ModifiedFileViewModel = new();
        public FileViewModel RemoveFileViewModel = new();
        private IJSObjectReference jsModule { get; set; }
        public bool isVisible { get; set; }
        #endregion

        #region Parameters
        [Parameter] public bool ShouldAutoScorll { get; set; }
        #endregion



        #region Methods


        private async ValueTask<ItemsProviderResult<FileViewModel>> LoadFileViewModels(ItemsProviderRequest request)
        {
             FileViewModels = await  _file.SearchInContent("sport");
            return new ItemsProviderResult<FileViewModel>(FileViewModels.Skip(request.StartIndex).Take(request.Count), FileViewModels.Count());
        }

        protected  async Task OnInitializedAsync()
        {


             FileViewModels = await _file.SearchInContent("sport");
            StateHasChanged();
            base.OnInitializedAsync();

        }

   
        protected  async Task OnParametersSetAsync()
        {
          
         
            base.OnParametersSetAsync();
        }


        private void OpenDialogCreateComment(FileViewModel p)
        {
            //var parameters = new DialogParameters();
            //parameters.Add("FileViewModel", p);
            //var options = new DialogOptions { CloseOnEscapeKey = true, };
            //DialogService.Show<FileViewModelCommentCreationDialog>("Créer un commentaire", parameters, options);
        }


        public void ToggleOverlay(bool value)
        {
            isVisible = value;
        }
        #endregion
    }
}

