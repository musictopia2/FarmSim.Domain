namespace FarmSim.Domain.Services.Worksites;
public class WorksiteBaselineBenefit
{
    public string Item { get; set; } = "";
    public double Chance { get; set; } //this means each worker increases the chances by this amount.  if over 100, then becomes 100 obviously.
    public bool Guarantee { get; set; }
    public int Quantity { get; set; } = 1; //defaults to 1 because its usually 1.
    public bool EachWorkerGivesOne { get; set; } //many items on tropic escape you get one for each worker sent.
    public bool Optional { get; set; } //barn nails and padlocks are optional.  so if doing focused runs, won't increase the chances for future nails or padlocks.
}