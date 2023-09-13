using System;
using Microsoft.AspNetCore.Components;

namespace Google_Like_Blazor.Components
{
	public partial class SearchBarItem
	{

            /// <summary>
            /// chaine de caractère de recherche
            /// </summary>
            public string SearchTerm { get; set; }
            /// <summary>
            /// Parametre: notification de chaine de caractère de recherche
            /// </summary>
            [Parameter]
            public EventCallback<string> OnSearchChanged { get; set; }

        

    }

}

