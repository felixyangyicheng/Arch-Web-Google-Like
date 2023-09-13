
namespace Google_Like_Blazor.RequestFeatures
{
	public class BaseItemParameters
	{
		const int maxPageSize = 70;
		public int PageNumber { get; set; } = 1;
		private int _pageSize = 5;
		public	int PageSize
		{

			get
			{
				return _pageSize;

			}
			set
			{
				_pageSize = value > maxPageSize ? maxPageSize : value;
			}
		}

	}
}

