namespace FarmSim.Domain.Utilities;
public static class FarmExtensions
{
    extension(FarmKey farm)
    {
        public FarmKey AsMain => farm with { Slot = EnumFarmSlot.Main };
        public FarmKey AsCoin => farm with { Slot = EnumFarmSlot.Coin };
        public FarmKey AsCooperative => farm with { Slot = EnumFarmSlot.Cooperative };
        public bool IsMain => farm.Slot == EnumFarmSlot.Main;
        public bool IsCoin => farm.Slot == EnumFarmSlot.Coin;
        public bool IsCooperative => farm.Slot == EnumFarmSlot.Cooperative;
        public bool IsBaseline => farm.IsMain || farm.IsCooperative;
        public Guid CreateId(string entityKind, int index)
        {
            var main = farm.IsMain ? farm : farm.AsMain;

            // Use only stable fields that define the pair identity
            // (Slot is forced to main already by the line above)
            string pairKey = $"{main.ProfileId}|{main.Theme}";

            string key = string.Join("|", new[] { pairKey, entityKind, index.ToString() });
            var bytes = System.Text.Encoding.UTF8.GetBytes(key);
            var hash = System.Security.Cryptography.MD5.HashData(bytes);
            return new Guid(hash);
        }



    }
    private static string Norm(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();

}