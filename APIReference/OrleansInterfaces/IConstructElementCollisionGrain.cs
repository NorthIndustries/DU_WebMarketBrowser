using Orleans;

namespace NQ.Interfaces;
public interface IConstructElementCollisionGrain : IGrainWithIntegerKey
{
    Task SetCollidingElementList(ElementCollidingList elementCollidingList);
    Task SetCollidingElements(ElementColliding elementColliding);
}
