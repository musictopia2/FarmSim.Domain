namespace FarmSim.Domain.Services.Animals;
public class AnimalGrantModel
{
    public string AnimalName { get; set; } = ""; //suggested the animal name.
    public required ItemAmount OutputData { get; init; } //this is what you receive
    public required ItemAmount InputData { get; init; } //this is what is required.
}