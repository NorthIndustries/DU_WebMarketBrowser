namespace NQ.Interfaces;

public interface IBotCallsGrain : Orleans.IGrainWithIntegerKey
{
    Task BotGiveItems(NQ.PlayerId id, NQ.ItemAndQuantityList items);
}
