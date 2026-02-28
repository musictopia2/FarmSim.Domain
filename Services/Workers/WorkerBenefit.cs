namespace FarmSim.Domain.Services.Workers;
public class WorkerBenefit
{
    public string Item { get; set; } = "";
    public string Worksite { get; set; } = ""; // Where it applies
    public double ChanceModifier { get; set; }         // e.g., 0.1 = +10% chance  if setting 0 then the chances don't increase
    public bool GivesExtra { get; set; } //hint:  if 2 workers gives one extra blackberry, you still only receive 2 blackberries.
    public bool Guarantee { get; set; }
}