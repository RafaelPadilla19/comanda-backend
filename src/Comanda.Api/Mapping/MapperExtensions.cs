using RMapper.Core.Interfaces;

namespace Comanda.Api.Mapping;

public static class MapperExtensions
{
    /// <summary>Mapea una colección elemento por elemento usando RMapper.</summary>
    public static List<TDest> MapList<TSrc, TDest>(this IRMapper mapper, IEnumerable<TSrc> source)
        => source.Select(s => mapper.Map<TSrc, TDest>(s)).ToList();
}
