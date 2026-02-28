namespace FarmSim.Domain.Services.AdvancedSettings;
public class WorksiteWorkerPreferenceModel
{
    public string WorksiteLocation { get; set; } = "";
    public BasicList<string> PreferredWorkers { get; set; } = []; // ordered list of WorkerName
    public WorksiteWorkerPreferenceModel Clone() => new()
    {
        WorksiteLocation = WorksiteLocation,
        PreferredWorkers = PreferredWorkers.ToBasicList()
    };
}