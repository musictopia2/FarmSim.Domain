namespace FarmSim.Domain.Services.Store;
public class StoreServicesContext
{
    public required IStoreUiStateRepository UiStateRepository { get; init; }
}