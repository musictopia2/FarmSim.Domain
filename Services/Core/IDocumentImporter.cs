namespace FarmSim.Domain.Services.Core;
public interface IDocumentImporter<TDocument>
{
    Task ImportAsync(BasicList<TDocument> documents);
}