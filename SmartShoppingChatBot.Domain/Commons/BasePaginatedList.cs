namespace SmartShoppingChatBot.Domain.Commons;

public class BasePaginatedList<T> where T : class
{
    public IReadOnlyCollection<T> Items { get; set; }
    public int TotalItems { get; set; }
    public int PageIndex { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public BasePaginatedList()
    {
        Items = new List<T>();
    }
    public BasePaginatedList(IReadOnlyCollection<T> items, int totalItems, int pageIndex, int pageSize)
    {

        TotalItems = totalItems;
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        Items = items;
    }
}
