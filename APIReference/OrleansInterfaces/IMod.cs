using System;
using NQ;

public interface IMod
{
    /// Static mod name, don't screw up with this and return a constant
    string GetName();
    /// Initialize the mod if needed, called once per boot
    Task Initialize(IServiceProvider serviceProvider);
    /// Notify that a player triggered a mod action from the client
    Task TriggerAction(ulong playerId, ModAction action);
    /// Get available actions for given player, or null for nothing
    Task<ModInfo> GetModInfoFor(ulong playerId, bool isPlayerAdmin);
}