using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public interface ISchematicContainerGrain : IGrainWithIntegerKey
    {
        [AlwaysInterleave]
        Task<ElementId?> GetSchematicContainer();

        Task SetSchematicContainer(ElementId? schematic);

        [OneWay]
        Task WakeUpIndustriesInConstruct();
    }
}