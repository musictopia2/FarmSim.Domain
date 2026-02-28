namespace FarmSim.Domain.Utilities;
public static class FarmDatabaseExtensions
{
    extension<T>(IEnumerable<T> source)
        where T : class, IFarmDocumentModel
    {
        public bool HasAnyDocument(FarmKey farm) => source.Any(x => x.Farm == farm);
        public T GetSingleDocument(FarmKey farm) => source.Single(x => x.Farm == farm);
        public BasicList<T> GetDocuments(FarmKey farm) => source.Where(x => x.Farm == farm).ToBasicList();
    }
}