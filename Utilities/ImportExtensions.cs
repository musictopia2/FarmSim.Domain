namespace FarmSim.Domain.Utilities;
public static class ImportExtensions
{
    extension<T>(BasicList<T> items)
        where T : class, ISharedGuid
    {
        /// <summary>
        /// Assigns deterministic shared IDs to items by grouping on <paramref name="groupKeySelector"/>.
        /// Items with the same group key get an ordinal index (0..n-1) within that group.
        ///
        /// Important: The order within each group must be stable across Main/Coop.
        /// If you don't have a natural stable order, pass an <paramref name="withinGroupOrderBy"/> selector.
        /// </summary>
        public void AssignSharedIdsByGroupIndex(
            FarmKey farm,
            Func<T, string> groupKeySelector)
        {
            var groups = items
            .GroupBy(groupKeySelector)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                int index = 0;


                foreach (var item in group)
                {
                    item.Id = farm.CreateId(group.Key, index);
                    //setId(item, farm.CreateId(group.Key, index));
                    index++;
                }
            }
        }
    }
}
