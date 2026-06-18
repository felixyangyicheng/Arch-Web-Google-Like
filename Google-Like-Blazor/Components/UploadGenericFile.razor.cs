using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

using Tewr.Blazor.FileReader;


namespace Google_Like_Blazor.Components
{
    public partial class UploadGenericFile
    {
        [Inject] IFileRepo _file { get; set; } = null!;
        [Inject] IGridSfRepo gridSf { get; set; } = null!;
        [Inject] PdfTextCache _textCache { get; set; } = null!;

        [Inject] IFileReaderService fileReaderService { get; set; } = null!;
        [Inject] NavigationManager _navi { get; set; }
        [Inject] IDialogService DialogService { get; set; }
        [Inject] ISnackbar Snackbar { get; set; }
        [Inject] IJSRuntime _jsRuntime { get; set; }
        [Inject] public IConfiguration _config { get; set; }

        public IEnumerable<IFileReference> files { get; set; }
        [Parameter]
        public EventCallback<bool> OnUploadCompleted { get; set; }
        [Parameter] public string SelectedProject { get; set; }
        [Parameter] public int SelectedProjectId { get; set; }

        [Inject] protected Microsoft.AspNetCore.Hosting.IWebHostEnvironment? HostEnvironment { get; set; } //获取IWebHostEnvironment

        protected ElementReference UploadElement { get; set; }
        protected InputFile? inputFile { get; set; }

        private DotNetObjectReference<UploadGenericFile>? wrapper;

        private IJSObjectReference? module;
        private IJSObjectReference? dropInstance;

        protected string UploadPath = "";
        protected string? uploadstatus;
        long maxFileSize = 1024 * 1024 * 35;

        public bool uploadCompleted { get; set; } = true;

 

        protected class PdfUploadModel : FileModel
        {
            public int Progress { get; set; }
            public bool Uploaded { get; set; } = false;
            public bool Deleted { get; set; }
        }
        private ElementReference inputTypeFileElement;

        protected List<PdfUploadModel> PdfUploadModels { get; set; } = new List<PdfUploadModel>();

        protected override void OnAfterRender(bool firstRender)
        {
            if (!firstRender) return;
    
        }
        protected override async Task OnParametersSetAsync()
        {

    
            await base.OnParametersSetAsync();
        }
        protected async Task OnChange(InputFileChangeEventArgs e)
        {
            int i = 0;
            var selectedFiles = e.GetMultipleFiles(100);
            foreach (var item in selectedFiles)
            {
                i++;
                uploadCompleted = !uploadCompleted;
                await OnUploadCompleted.InvokeAsync(uploadCompleted);
                //if (item.ContentType == "application/pdf")
                //{
                //    Snackbar.Add($"Fichier {item.Name}, téléchargement en cours", Severity.Normal);
                    await OnSubmit(item);
                //}
                //else
                //{
                //    Snackbar.Add($"Fichier {item.Name}, n'est pas un fichier PDF", Severity.Error);
                //}
                uploadstatus += Environment.NewLine + $"[{i}]: " + item.Name;
            }
            Snackbar.Add($"document générique mis à jour", Severity.Success);    
        }

        protected async Task OnSubmit(IBrowserFile efile)
        {
            if (efile == null) return;
            var uploadElement = new PdfUploadModel();
            uploadElement.Progress = 0;
            PdfUploadModels.Add(uploadElement);
            var buffer = new byte[1024 * 512];
            int count;
            int totalCount = 0;
            //var tempfilename = Path.Combine(UploadPath, efile.Name);
            //await using FileStream fs = new(efile.Name, FileMode.Open);
            using var stream = efile.OpenReadStream(maxFileSize);
            var finalBuffer = new byte[stream.Length];

            while ((count = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                Buffer.BlockCopy(buffer, 0, finalBuffer, totalCount, count);
                totalCount += count;
                uploadElement.Progress = (int)(totalCount * 100.0 / stream.Length);
                StateHasChanged();
            }

            uploadElement.Type = efile.ContentType;
            uploadElement.FileName = efile.Name;

            uploadElement.Content = finalBuffer;

            var FileDto = new FileModel
            {
                FileName = uploadElement.FileName,
                Type = uploadElement.Type,
 
                Content = uploadElement.Content,
            };


            //var result = await gridSf.UploadFile(FileDto.FileName,efile.ContentType, FileDto.Content);
            //if (!string.IsNullOrEmpty(result.ToString()))
            //{
            //    uploadElement.Uploaded = true;
            //    uploadCompleted = true;
            //    StateHasChanged();
            //    uploadCompleted = !uploadCompleted;
            //    StateHasChanged();
            //    await OnUploadCompleted.InvokeAsync(uploadCompleted);
            //    Snackbar.Add($"{uploadElement.FileName} added", Severity.Success);

            //}

            var isExistOld = await _file.GetByNameAsync(FileDto.FileName);

            if (isExistOld != null)
            {
                FileDto.Id = isExistOld.Id;
                var result = _file.UpdateAsync(isExistOld.Id, FileDto);
                StateHasChanged();
                if (!string.IsNullOrEmpty(result.ToString()))
                {
                    uploadElement.Uploaded = true;
                    uploadCompleted = true;
                    StateHasChanged();
                    uploadCompleted = !uploadCompleted;
                    StateHasChanged();
                    await OnUploadCompleted.InvokeAsync(uploadCompleted);
                    Snackbar.Add($"{uploadElement.FileName} updated", Severity.Success);
                    // Pre-warm PDF text cache so first search is instant
                    if (FileDto.Type.Contains("pdf"))
                        PreCachePdfText(FileDto);
                }
            }
            else
            {
                var result = await _file.CreateAsync(FileDto);
                if (!string.IsNullOrEmpty(result.ToString()))
                {
                    uploadElement.Uploaded = true;
                    uploadCompleted = true;
                    StateHasChanged();
                    uploadCompleted = !uploadCompleted;
                    StateHasChanged();
                    await OnUploadCompleted.InvokeAsync(uploadCompleted);
                    Snackbar.Add($"{uploadElement.FileName} added", Severity.Success);
                    // Pre-warm PDF text cache so first search is instant
                    if (FileDto.Type.Contains("pdf"))
                        PreCachePdfText(FileDto);
                }
            }
            // await stream.CopyToAsync(fs);
            StateHasChanged();
        }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;

            module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/UploadGenericFile.razor.js");
            wrapper = DotNetObjectReference.Create(this);
            dropInstance = await module.InvokeAsync<IJSObjectReference>("init", wrapper, UploadElement, inputFile!.Element);
        }
        /// <summary>
        /// ⚡ Pre-extract and cache PDF text at upload time.
        /// The first search on this file will hit the cache instead of re-parsing the PDF.
        /// </summary>
        private void PreCachePdfText(FileModel file)
        {
            try
            {
                if (!_textCache.TryGet(file.Id, out _))
                {
                    using var pdf = UglyToad.PdfPig.PdfDocument.Open(file.Content);
                    var pages = pdf.GetPages().Select(p => p.Text).ToArray();
                    _textCache.Set(file.Id, pages);
                }
            }
            catch
            {
                // Not a valid PDF or extraction failed — don't block the upload
            }
        }

        #region JSInvokable DropAlert

        [JSInvokable]
        public void DropAlert(string msg)
        {
            uploadstatus += Environment.NewLine + $"[!Alert!]: " + msg;
            StateHasChanged();
        }
        #endregion
        #region IAsyncDisposable

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            if (dropInstance != null)
            {
                await dropInstance.InvokeVoidAsync("dispose");
                await dropInstance.DisposeAsync();
            }

            if (wrapper != null)
            {
                wrapper.Dispose();
            }

            if (module != null)
            {
                await module.DisposeAsync();
            }
        }
        #endregion

    }
}
