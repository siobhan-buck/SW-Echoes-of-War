namespace SWLOR.Game.Server.Service.PlayerListService
{
    /// <summary>
    /// Opt-in flags a player shows next to their character in the player list.
    /// Values are persisted; never renumber or reuse them.
    /// </summary>
    public enum PlayerListFlagType
    {
        [PlayerListFlag("General RP", "General RP: looking for roleplay. Please approach.", "pl_flag_rp")]
        GeneralRP = 1,

        [PlayerListFlag("PvP", "PvP: seeking player-versus-player conflict. Reminder that PvP is always a potential, even if this flag is not checked.", "pl_flag_pvp")]
        PvP = 2,

        [PlayerListFlag("DM", "DM: looking for DM assistance.", "pl_flag_dm")]
        DM = 3,

        [PlayerListFlag("Crafting RP", "Crafting RP: offering or looking for crafting services.", "pl_flag_craft")]
        Crafting = 4,
    }
}
