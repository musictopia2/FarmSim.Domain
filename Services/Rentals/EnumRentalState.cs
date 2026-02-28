namespace FarmSim.Domain.Services.Rentals;
public enum EnumRentalState
{
    Active = 0,            // time not expired; rental should be usable
    ExpiredPending = 1    // time expired, but domain hasn't finalized yet (e.g., must collect first)
}