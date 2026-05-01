using UnityEngine;

public enum DrugQuality
{
    Cut = 0,
    Standard = 1,
    Pure = 2
}

// Quality alters every economic axis of a drug stack: cost, sell ratio, slot bulk, and heat per
// unit. The shape of the trade-off is intentional: Cut is cheap, bulky, and noisy; Pure is
// expensive, compact, and quiet. Players choose between volume (Cut) and capital efficiency (Pure).
public static class DrugQualityX
{
    public static float BuyMult(DrugQuality q)
    {
        switch (q)
        {
            case DrugQuality.Cut: return 0.65f;
            case DrugQuality.Pure: return 1.6f;
            default: return 1f;
        }
    }

    // Applied AFTER the dealer's sellRatio so it stacks: a ratio-0.55 dealer selling Pure ends up
    // at 0.55 × 1.55 = ~0.85 of the (already 1.6×) buy price.
    public static float SellMult(DrugQuality q)
    {
        switch (q)
        {
            case DrugQuality.Cut: return 0.85f;
            case DrugQuality.Pure: return 1.55f;
            default: return 1f;
        }
    }

    // Pure is denser → fewer slots per stack. Cut is cut with filler → bulkier.
    public static float UnitsPerSlotMult(DrugQuality q)
    {
        switch (q)
        {
            case DrugQuality.Cut: return 1.4f;
            case DrugQuality.Pure: return 0.8f;
            default: return 1f;
        }
    }

    // Pure trades hands in smaller batches → less heat per unit moved. Cut is street-level → loud.
    public static float HeatPerUnitMult(DrugQuality q)
    {
        switch (q)
        {
            case DrugQuality.Cut: return 1.3f;
            case DrugQuality.Pure: return 0.7f;
            default: return 1f;
        }
    }

    public static string Prefix(DrugQuality q)
    {
        switch (q)
        {
            case DrugQuality.Cut: return "Cut ";
            case DrugQuality.Pure: return "Pure ";
            default: return "";
        }
    }

    // UI accent color for quality badges.
    public static Color BadgeColor(DrugQuality q)
    {
        switch (q)
        {
            case DrugQuality.Cut: return new Color(0.65f, 0.65f, 0.65f);
            case DrugQuality.Pure: return new Color(1f, 0.85f, 0.2f);
            default: return new Color(0.85f, 0.85f, 0.85f);
        }
    }

    // Hex equivalent of BadgeColor for TMP rich-text.
    public static string BadgeHex(DrugQuality q)
    {
        switch (q)
        {
            case DrugQuality.Cut: return "#A6A6A6";
            case DrugQuality.Pure: return "#FFD93B";
            default: return "#D9D9D9";
        }
    }
}
