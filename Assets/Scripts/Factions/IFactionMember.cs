namespace Factions
{
    /// <summary>
    /// Exposes the faction of a world entity to systems that operate on targets,
    /// without making those systems depend on a particular lifetime-scope type.
    /// </summary>
    public interface IFactionMember
    {
        FactionConfig Faction { get; }
    }
}
