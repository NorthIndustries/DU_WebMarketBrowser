using NQ;
using NQutils.Def;

#nullable enable

namespace Backend
{
    public delegate void OnChange();

    public interface IGameplayBank
    {
        ulong IdFor(string name);
        ulong IdFor<TObject>() where TObject : GameplayObject;

        /// <summary>
        /// Get a GameplayDefinition for a given item ID.
        /// </summary>
        /// <param name="id">Item ID.</param>
        /// <returns>The definition, null if not found.</returns>
        IGameplayDefinition? GetDefinition(ulong id);

        /// <summary>
        /// Get a GameplayDefinition for a given item name.
        /// This is totally deprecated with a constant string argument, don't use that in new code.  Prefer this:
        /// <code>
        ///     bank.GetDefinition<NQutils.Def.Character>()
        /// </code>
        /// </summary>
        /// <param name="name">Item name.</param>
        /// <returns>The definition, null if not found.</returns>
        IGameplayDefinition? GetDefinition(string name);

        IEnumerable<IGameplayDefinition> GetDefinitions();
        IEnumerable<IGameplayDefinition> GetInventoryDefinitions();

        VolumeAndMass GetDeltaVolumeAndMass(ulong id, long deltaQuantity);
        long GetQuantity(ulong id, double volume);
        void RegisterOnChange(OnChange cb);
        Task NotifyChangedAsync();

        Task InitializeAsync(string? yamlOverride = null);

        double VolumeFor(ulong id, ulong quantity);
        // VolumeFor but quantity expressed in market units.
        double VolumeForMarket(ulong id, ulong quantity);

        // In-game quantity from value in GD file. liters->quantity for Materials, no op for the rest
        long QuantityFromGDValue(ulong id, long quantity);
        IEnumerable<ItemAndQuantity> GetDefaultTools();
        IEnumerable<ItemAndQuantity> GetDefaultInventory();
        IEnumerable<ItemAndQuantity> GetDefaultBotsInventory();

        /// <summary>
        /// Export the whole bank (as a multi-document YAML).
        /// </summary>
        Task<string> Export();
        Task<string> GetAsString();
        string Signature();

        /// <summary>
        /// Validates and then import items from the given document.
        /// </summary>
        /// <param name="deleteMissing">If true, items previously stored in the bank not present in the YAML will be deleted.</param>
        Task Import(string yaml, bool deleteMissing);
        void Validate(string yaml);

        Task Delete(ulong id, bool reinitialize = false);

        bool IsInitialized();

        string Normalize(string yaml);
        Task PreparePropertyList();
        Task<string> GetYaml(ulong id);
        Task<Dictionary<ulong, string>> GetYaml(ulong[] ids);
        SortedDictionary<int, IGameplayDefinition> BFSSort(IGameplayDefinition def);
    }

    public readonly struct VolumeAndMass
    {
        public readonly double Volume;
        public readonly double Mass;

        public VolumeAndMass(double volume, double mass)
        {
            this.Volume = Math.Round(volume, 6);
            this.Mass = Math.Round(mass, 6);
        }

        public static VolumeAndMass Zero => new();

        public static VolumeAndMass operator +(VolumeAndMass a, VolumeAndMass b)
            => new(a.Volume + b.Volume, a.Mass + b.Mass);
        public static VolumeAndMass operator -(VolumeAndMass a, VolumeAndMass b)
            => new(a.Volume - b.Volume, a.Mass - b.Mass);
    }
}
