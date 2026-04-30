public static class CityEventManager
{
    public enum CityEvent { None, Lockdown, Festival, Shortage }

    private const float LockdownChance = 0.10f;
    private const float FestivalChance = 0.10f;
    private const float ShortageChance = 0.10f;

    public const float LockdownPriceMult = 0.55f;
    public const float LockdownHeatMult  = 2.0f;
    public const float FestivalSellMult  = 2.0f;
    public const float ShortagePriceMult = 1.8f;
    public const float ShortageHeatMult  = 1.3f;

    private static float Roll(string key)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in key) hash = hash * 31 + c;
            return (float)new System.Random(hash).NextDouble();
        }
    }

    public static CityEvent GetEventForCity(string cityName)
    {
        float u = Roll($"cityevent:{PriceService.RunSeed}:{PriceService.InGameDay}:{cityName}");
        if (u < LockdownChance)                                   return CityEvent.Lockdown;
        if (u < LockdownChance + FestivalChance)                  return CityEvent.Festival;
        if (u < LockdownChance + FestivalChance + ShortageChance) return CityEvent.Shortage;
        return CityEvent.None;
    }

    public static float GetHeatMult(string cityName)
    {
        return GetEventForCity(cityName) switch
        {
            CityEvent.Lockdown => LockdownHeatMult,
            CityEvent.Shortage => ShortageHeatMult,
            _                  => 1f
        };
    }
}
