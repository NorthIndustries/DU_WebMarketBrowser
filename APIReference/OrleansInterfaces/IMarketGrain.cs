namespace NQ.Interfaces
{
    public interface IMarketGrain : Orleans.IGrainWithIntegerKey
    {
        Task<MarketList> MarketGetList(ConstructId id, PlayerId pid);
        Task<MarketInfo> MarketOpen(MarketAccessPoint point, PlayerId pid);
        Task MarketClose(MarketAccessPoint point, PlayerId pid);
        Task<MarketOrders> MarketSelectItem(MarketSelectRequest req, PlayerId pid);
        Task<MarketOrders> MarketGetMyOrders(MarketSelectRequest req, PlayerId pid);
        Task<MarketOrders> MarketInstantOrder(MarketRequest req, PlayerId pid);
        Task<MarketOrder> MarketPlaceOrder(MarketRequest req, PlayerId pid);
        Task<MarketOrder> MarketUpdateOrder(MarketRequest req, PlayerId pid);
        Task MarketCancelOrder(MarketOrder req, PlayerId pid);
        Task<MarketStorageInfoEx> MarketContainerGetMyContent(MarketSelectRequest req, PlayerId pid);
        Task<MarketStorageMoveInfo> MarketStorageMove(MarketStorageMoveInfo moveInfo, PlayerId pid);
        Task<ElementId> CreatePackage(PackageMarketCreate pmc, PlayerId playerId);
        Task ExpandPackage(PackageMarketExpand pme, PlayerId playerId);
    }

}
