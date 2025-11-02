using Orleans;

namespace NQ.Interfaces
{
    public interface IBookmarkGrain : IGrainWithIntegerKey
    {
        Task<BookmarkList> GetBookmarkList(PlayerId player);
        Task AddBookmark(PlayerId player, Bookmark bookmark);
        Task RemoveBookmark(PlayerId player, BookmarkId bookmark);
        Task UpsertBookmark(PlayerId player, Bookmark bookmark);
    }
}
