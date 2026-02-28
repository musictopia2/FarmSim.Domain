namespace FarmSim.Domain.Services.Animals;
public class AnimalView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsRental { get; set; }
    public bool IsFast { get; set; }
    public bool MaxBenefits { get; set; }
}