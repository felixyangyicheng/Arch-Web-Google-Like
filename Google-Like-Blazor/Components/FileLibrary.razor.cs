
using Microsoft.JSInterop;

using System.IO;

namespace Google_Like_Blazor.Components
{
    public partial class FileLibrary
    {
        #region properties

        /// <summary>
        /// dependency injection service pdf
        /// </summary>
        [Inject] IFileRepo _file { get; set; }
        /// <summary>
        /// dependency injection MudBlazor dialogservice
        /// </summary>
        [Inject] IDialogService DialogService { get; set; }
        /// <summary>
        /// dependency injection MudBlazor Snackbar
        /// </summary>
        [Inject] ISnackbar Snackbar { get; set; }
        /// <summary>
        /// projectname parameter (url path)
        /// </summary>


        [Parameter] public bool UploadCompleted { get; set; } 
        [Parameter] public EventCallback<bool> UploadCompletedChanged { get; set; }
        private string searchStringInit = "";
        /// <summary>
        /// page loading status
        /// </summary>
        private bool loading { get; set; } = false;

        /// <summary>
        /// selected item in MudBlazor table
        /// </summary>
        private FileModel selectedItem = null;
        /// <summary>
        /// FileModel to be edited
        /// </summary>
        private FileModel elementBeforeEdit;
        /// <summary>
        /// Edit trigger
        /// </summary>
        private TableEditTrigger editTrigger = TableEditTrigger.RowClick;

        /// <summary>
        /// items in MudBlazor table
        /// </summary>
        protected List<FileModel> FileModels { get; set; } = new();

        #endregion
        #region methods


        private async Task ShowFile(string fileName)
        {


        }
        private bool FilterFunc1(FileModel element) => FilterFunc(element, searchStringInit);

        private bool FilterFunc(FileModel element, string searchString)
        {
            if (string.IsNullOrWhiteSpace(searchString))
                return true;

            if (element.FileName.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
        protected override async Task OnParametersSetAsync()
        {
            loading = true;

            StateHasChanged();

            loading = false;

            StateHasChanged();
            base.OnParametersSetAsync();
        }
        #endregion methods

    }
}
