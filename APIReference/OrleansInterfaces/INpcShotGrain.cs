using NQ.RDMS;
using Orleans;
using Orleans.Concurrency;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NQ.Interfaces
{
    public interface INpcShotGrain: IGrainWithIntegerKey
    {
        Task NpcShotComputation(NpcShotResult shot);
        [OneWay]
        Task Fire(string shooterName, Vec3 shooterPosition, ulong shooterConstructId, ulong shooterConstructSize, ulong targetConstructId, Vec3 target, SentinelWeapon weapon, double crossSection, Vec3 localHitPosition, ulong impactVoxelMaterialId=0, ulong impactElementId=0, ulong impactElementType=0, ulong reportDestructionTo = 0, bool isFallback = false);
    }
}
    